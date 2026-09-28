import json
from pathlib import Path

script_directory = Path(__file__).resolve().parent
root_directory = script_directory.parent
system_prompt_path = root_directory / "SYSTEM_PROMPT.md"
tools_path = root_directory / "TOOLS.json"
few_shot_prompts_path = root_directory / "FEW_SHOT_PROMPTS.json"
dataset_path = root_directory / "training.jsonl"

def build_training_examples(system_prompt, tools, examples):
    if all("messages" in example for example in examples):
        conversations = [example["messages"] for example in examples]
    else:
        conversations = [examples[i:i + 2] for i in range(0, len(examples), 2)]

    return [
        {
            "messages": [
                {
                    "role": "system",
                    "content": system_prompt,
                },
                *conversation,
            ],
            "tools": tools,
        }
        for conversation in conversations
    ]


with system_prompt_path.open(encoding="utf-8") as f:
    system_prompt = f.read()

with tools_path.open(encoding="utf-8") as f:
    tools = json.load(f)

with few_shot_prompts_path.open(encoding="utf-8") as f:
    examples = json.load(f)

training_examples = build_training_examples(system_prompt, tools, examples)

with dataset_path.open("w", encoding="utf-8") as f:
    for example in training_examples:
        f.write(json.dumps(example) + "\n")

print(f"Wrote {len(training_examples)} examples to {dataset_path}")
