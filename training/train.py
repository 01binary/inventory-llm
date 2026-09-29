# Training Notebook for Gemma3 (12B) on Kaggle
# From https://www.kaggle.com/notebooks/welcome?src=https%3A%2F%2Fgithub.com%2Funslothai/notebooks/blob/main/nb/Kaggle-Gemma3_(4B).ipynb

# Install Dependencies

!pip install pip3-autoremove
!pip install torch torchvision torchaudio xformers --index-url https://download.pytorch.org/whl/cu128
!pip install unsloth
!pip install --no-deps --upgrade "torchao>=0.16.0"
!pip install --upgrade --no-cache-dir transformers==4.57.6 huggingface_hub==0.36.2
!pip install --no-deps trl==0.22.2
!pip install --upgrade --no-cache-dir --no-deps unsloth_zoo

# Load Model

from unsloth import FastLanguageModel

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name = "unsloth/gemma-3-12b-it-unsloth-bnb-4bit",
    # "unsloth/gemma-3-1b-it-unsloth-bnb-4bit",
    # "unsloth/gemma-3-4b-it-unsloth-bnb-4bit",
    # "unsloth/gemma-3-12b-it-unsloth-bnb-4bit",
    # "unsloth/gemma-3-27b-it-unsloth-bnb-4bit",
    # "unsloth/Llama-3.1-8B",
    # "unsloth/Llama-3.2-3B",
    # "unsloth/Llama-3.3-70B",
    # "unsloth/mistral-7b-instruct-v0.3",
    # "unsloth/Phi-4",
    max_seq_length = 8192,   # System prompt (incl. tool schema text) + tool-call turns run ~4k tokens on the longest examples
    load_in_4bit = True,     # 4bit uses much less memory
    load_in_8bit = False,    # A bit more accurate, uses 2x memory
    full_finetuning = False, # We have full finetuning now!
    # token = "YOUR_HF_TOKEN",      # HF Token for gated models
)

model = FastModel.get_peft_model(
    model,
    finetune_vision_layers     = False, # Turn off for just text!
    finetune_language_layers   = True,  # Should leave on!
    finetune_attention_modules = True,  # Attention good for GRPO
    finetune_mlp_modules       = True,  # Should leave on always!

    r = 8,           # Larger = higher accuracy, but might overfit
    lora_alpha = 8,  # Recommended alpha == r at least
    lora_dropout = 0,
    bias = "none",
    random_state = 3407,
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
# Convert to format expected by Jinja tokenizer template

# The system message (see data-prep.py) includes the tool-calling protocol and
# schema as plain text rather than a `tools=` template kwarg or a "tool" role -
# Gemma's chat template has neither and strictly enforces alternating
# user/assistant turns, unlike Qwen's Hermes-style tool-calling support. Save
# it now so inference can reuse it after remove_columns drops "messages" below.
system_prompt = dataset[0]["messages"][0]["content"]

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
    ),
)

trainer_stats = trainer.train()

# Inference
# Must include the same system prompt every training example used, otherwise the
# chat template produces a prompt shape the model never saw while training.

from transformers import TextStreamer

messages = [
    {"role" : "system", "content" : system_prompt},
    {"role" : "user", "content" : "Order a dozen tortillas and 6 salsa verde."}
]

inputs = tokenizer.apply_chat_template(
    messages,
    add_generation_prompt = True, # Must add for generation
    tokenize = True,
    return_tensors = "pt",
    return_dict = True,
)

_ = model.generate(
    **inputs.to("cuda"),
    max_new_tokens = 64, # Increase for longer outputs!
    # Recommended Gemma-3 settings!
    temperature = 1.0, top_p = 0.95, top_k = 64,
    streamer = TextStreamer(tokenizer, skip_prompt = True),
)

# Push Model

from huggingface_hub import login

login()

model.push_to_hub_gguf(
    "valnovytskyy/inventory-gemma3-12B",
    tokenizer,
    quantization_method = "f16"
)
