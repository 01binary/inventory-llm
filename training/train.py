# Training Notebook for Qwen3 (14B) on Kaggle
# From https://unsloth.ai/docs/get-started/unsloth-notebooks#kaggle-notebooks
# Using Qwen3 (14B): https://www.kaggle.com/notebooks/welcome?src=https%3A%2F%2Fgithub.com%2Funslothai/notebooks/blob/main/nb/Kaggle-Qwen3_(14B).ipynb

# Install Dependencies

!pip install pip3-autoremove
!pip install torch torchvision torchaudio xformers --index-url https://download.pytorch.org/whl/cu128
!pip install unsloth
!pip install --no-deps --upgrade "torchao>=0.16.0"
!pip install --upgrade --no-cache-dir transformers==4.57.6 huggingface_hub==0.36.2
!pip install --no-deps trl==0.22.2
!pip install --upgrade --no-cache-dir --no-deps unsloth_zoo

# Load Model
# unsloth/Qwen3-1.7B-unsloth-bnb-4bit
# unsloth/Qwen3-4B-unsloth-bnb-4bit
# unsloth/Qwen3-8B-unsloth-bnb-4bit
# unsloth/Qwen3-14B-unsloth-bnb-4bit
# unsloth/Qwen3-32B-unsloth-bnb-4bit
# unsloth/gemma-3-12b-it-unsloth-bnb-4bit
# unsloth/Phi-4
# unsloth/Llama-3.1-8B
# unsloth/Llama-3.2-3B
# unsloth/orpheus-3b-0.1-ft-unsloth-bnb-4bit

from unsloth import FastLanguageModel

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name = "unsloth/Qwen3-14B-unsloth-bnb-4bit",
    max_seq_length = 2048,   # Context length - can be longer, but uses more memory
    load_in_4bit = True,     # 4bit uses much less memory
    load_in_8bit = False,    # A bit more accurate, uses 2x memory
    full_finetuning = False, # We have full finetuning now!
    # token = "YOUR_HF_TOKEN",      # HF Token for gated models
)

model = FastLanguageModel.get_peft_model(
    model,
    r = 32,           # Choose any number > 0! Suggested 8, 16, 32, 64, 128
    target_modules = ["q_proj", "k_proj", "v_proj", "o_proj",
                      "gate_proj", "up_proj", "down_proj",],
    lora_alpha = 32,  # Best to choose alpha = rank or rank*2
    lora_dropout = 0, # Supports any, but = 0 is optimized
    bias = "none",    # Supports any, but = "none" is optimized
    # [NEW] "unsloth" uses 30% less VRAM, fits 2x larger batch sizes!
    use_gradient_checkpointing = "unsloth", # True or "unsloth" for very long context
    random_state = 3407,
    use_rslora = False,   # We support rank stabilized LoRA
    loftq_config = None,  # And LoftQ
)

# Load Dataset
# https://unsloth.ai/docs/get-started/fine-tuning-llms-guide/datasets-guide

from datasets import load_dataset

dataset = load_dataset(
    "json", # JSON builder reads one JSON object per line
    data_files="/kaggle/input/datasets/valnovytskyy/inventory-prompts/training.jsonl",
    split="train" # No splitting
)

# Transform Dataset
# Conver to format expected by Jinja tokenizer template

def format_chat(example):
    return {
        # Run Jinja template stored in tokenizer.chat_template
        "text": tokenizer.apply_chat_template(
            example["messages"],         # Over all messages
            tokenize=False,              # Return strings instead of token IDs
            add_generation_prompt=False, # Training instead of Inference
        )
    }

dataset = dataset.map(format_chat, remove_columns=["messages"])

dataset
dataset[0]

# Train
# https://huggingface.co/docs/trl/en/sft_trainer

from trl import SFTTrainer, SFTConfig

trainer = SFTTrainer(
    model = model,
    tokenizer = tokenizer,
    train_dataset = dataset,
    eval_dataset = None, # Can set up evaluation!
    args = SFTConfig(
        dataset_text_field = "text",
        per_device_train_batch_size = 2,
        gradient_accumulation_steps = 4, # Use GA to mimic batch size!
        warmup_steps = 5,
        # num_train_epochs = 1, # Set this for 1 full training run.
        max_steps = 30,
        learning_rate = 2e-4, # Reduce to 2e-5 for long training runs
        logging_steps = 1,
        optim = "adamw_8bit",
        weight_decay = 0.001,
        lr_scheduler_type = "linear",
        seed = 3407,
        report_to = "none", # Use TrackIO/WandB etc
        padding_free  = False, # Set to True if > 17 GB VRAM
    ),
)

trainer_stats = trainer.train()

# Inference

messages = [
    {"role" : "user", "content" : "Do I have candied ham in my inventory?"}
]

text = tokenizer.apply_chat_template(
    messages,
    tokenize = False,
    add_generation_prompt = True, # Must add for generation
    enable_thinking = False, # Disable thinking
)

from transformers import TextStreamer
_ = model.generate(
    **tokenizer(text, return_tensors = "pt").to("cuda"),
    max_new_tokens = 256, # Increase for longer outputs!
    temperature = 0.7, top_p = 0.8, top_k = 20, # For non thinking
    streamer = TextStreamer(tokenizer, skip_prompt = True),
)

# Push Model

from huggingface_hub import login

login()

model.push_to_hub_gguf(
    "valnovytskyy/inventory-qwen3-14B",
    tokenizer,
    quantization_method = "f16"
)
