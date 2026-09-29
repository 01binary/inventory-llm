import json
from pathlib import Path

script_directory = Path(__file__).resolve().parent
root_directory = script_directory.parent
system_prompt_path = root_directory / "SYSTEM_PROMPT.md"
tools_path = root_directory / "TOOLS.json"
few_shot_prompts_path = root_directory / "FEW_SHOT_PROMPTS.json"
dataset_path = root_directory / "training.jsonl"

# Tool calls are written as plain <tool_call>/<tool_response> text inside ordinary
# user/assistant turns rather than a dedicated "tool" role or a `tools=` template
# kwarg, because not every base model's chat template supports those (e.g. Gemma 3
# has no tool role and strictly enforces alternating user/assistant turns). Writing
# the protocol as plain text keeps the dataset portable across any base model listed
# in train.py.
def render_tools_block(tools):
    lines = [
        "# Tools",
        "",
        "You can call the following functions to help answer the user. To call one, "
        "respond with only this and nothing else:",
        "<tool_call>",
        '{"name": "<function-name>", "arguments": <arguments-as-json>}',
        "</tool_call>",
        "",
        "After a tool call, its result will be given to you wrapped in "
        "<tool_response></tool_response> tags. Use that result to answer the user "
        "in plain language - never show raw JSON or tags to the user.",
        "",
        "Available functions:",
    ]
    for tool in tools:
        function = tool["function"]
        lines.append(json.dumps(function, ensure_ascii=False))
    return "\n".join(lines)


def build_training_examples(system_prompt, examples):
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
        }
        for conversation in conversations
    ]


with system_prompt_path.open(encoding="utf-8") as f:
    system_prompt = f.read()

with tools_path.open(encoding="utf-8") as f:
    tools = json.load(f)

system_prompt = system_prompt + "\n\n" + render_tools_block(tools)

with few_shot_prompts_path.open(encoding="utf-8") as f:
    examples = json.load(f)

training_examples = build_training_examples(system_prompt, examples)

with dataset_path.open("w", encoding="utf-8") as f:
    for example in training_examples:
        f.write(json.dumps(example) + "\n")

print(f"Wrote {len(training_examples)} examples to {dataset_path}")
