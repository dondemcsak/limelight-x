# Intermediate Representation (IR)

## Purpose
The Intermediate Representation (IR) is the canonical, linear execution plan produced by the IR compiler.  
It is the bridge between the normalized AST and the evaluator.

The IR must be:

- deterministic  
- explicit  
- linear (no branching, no loops)  
- free of ambiguity  
- free of language‑level constructs (pronouns, names, implicit inputs)  

The evaluator must be able to execute the IR **without consulting the AST**.

---

# 1. Overview

The IR is a **vector of operations**, executed in order:

```
[
  IR::Load { ... },
  IR::Extract { ... },
  IR::Summarize { ... },
  ...
]
```

Each operation:

- has explicit inputs  
- references only earlier results  
- contains no unresolved names or pronouns  
- contains no Bind nodes  
- contains no NamedVariable nodes  

The IR compiler is responsible for converting normalized AST nodes into IR nodes with explicit `$N` references.

---

# 2. IR Reference Model

Each IR operation produces a result stored at an index:

```
$0 = result of IR[0]
$1 = result of IR[1]
$2 = result of IR[2]
...
```

### Rules

1. `$N` may reference only earlier operations (`N < current_index`).  
2. `$N` must be used exactly as produced by the IR compiler.  
3. No symbolic names appear in the IR.  
4. No pronouns appear in the IR.  
5. No implicit inputs appear in the IR.

---

# 3. IR Operations

The IR supports six operations:

- `Load`
- `Extract`
- `Summarize`
- `Translate`
- `Rewrite`
- `Format`

Each operation is defined below.

---

# 3.1 IR::Load

```
IR::Load {
    path: String
}
```

### Description
Reads a file from disk and produces its contents as a string.

### Fields
- `path`: absolute or relative file path

### Example
```
Load { path: "article.txt" }
```

---

# 3.2 IR::Extract

```
IR::Extract {
    target: String,
    input: IRRef
}
```

### Description
Extracts structured information (e.g., entities) from the input text.

### Fields
- `target`: extraction target (e.g., `"entities"`)
- `input`: `$N` reference to earlier result

### Example
```
Extract { target: "entities", input: "$0" }
```

---

# 3.3 IR::Summarize

```
IR::Summarize {
    input: IRRef,
    prompt: Option<String>
}
```

### Description
Summarizes the input text using either a built‑in prompt or a custom prompt.

### Fields
- `input`: `$N` reference  
- `prompt`: `None` for built‑in, `Some("<string>")` for custom

### Example
```
Summarize { input: "$1", prompt: Some("Summarize in 3 bullets.") }
```

---

# 3.4 IR::Translate

```
IR::Translate {
    input: IRRef,
    language: String,
    prompt: Option<String>
}
```

### Description
Translates the input text into a target language.

### Fields
- `input`: `$N` reference  
- `language`: target language (e.g., `"French"`)  
- `prompt`: optional custom prompt

### Example
```
Translate { input: "$0", language: "French", prompt: None }
```

---

# 3.5 IR::Rewrite

```
IR::Rewrite {
    input: IRRef,
    prompt: Option<String>
}
```

### Description
Rewrites the input text using either a built‑in or custom prompt.

### Fields
- `input`: `$N` reference  
- `prompt`: optional custom prompt

### Example
```
Rewrite { input: "$2", prompt: None }
```

---

# 3.6 IR::Format

```
IR::Format {
    input: IRRef,
    target: String
}
```

### Description
Formats the input text into a target representation (e.g., `"JSON"`).

### Fields
- `input`: `$N` reference  
- `target`: output format

### Example
```
Format { input: "$1", target: "JSON" }
```

---

# 4. IRRef

```
type IRRef = String   // always "$<index>"
```

### Rules

- Must be formatted as `"$<number>"`  
- Must reference a valid earlier index  
- Must not reference future operations  
- Must not contain symbolic names  

---

# 5. IR Compiler Rules

The IR compiler receives a **normalized AST** and produces a vector of IR nodes.

### 5.1 InputRef Mapping

The normalized AST contains only:

```
InputRef::PreviousResult
InputRef::Resource(String)
```

The IR compiler maps these to IRRefs:

- `PreviousResult` → `"$<last_index>"`
- `Resource(name)` → resolved to the most recent Load of that resource

### 5.2 Operation Ordering

IR operations must appear in the same order as the normalized AST.

### 5.3 Prompt Handling

- Expression holes become `prompt = Some("<string>")`
- Missing prompts become `prompt = None`

### 5.4 No NamedVariable

The IR compiler must never receive or emit NamedVariable nodes.

### 5.5 No Bind

Bind statements are removed by the normalizer and must not appear in the IR.

---

# 6. Examples

## Example 1: Load → Extract → Summarize

### Normalized AST
```
[
  Load { path: "article.txt" },
  Extract { target: "entities", input: PreviousResult },
  Summarize { input: PreviousResult, prompt: None }
]
```

### IR
```
[
  Load { path: "article.txt" },
  Extract { target: "entities", input: "$0" },
  Summarize { input: "$1", prompt: None }
]
```

---

## Example 2: Summarize with custom prompt

### Normalized AST
```
Summarize { input: PreviousResult, prompt: Some("Summarize in 3 bullets.") }
```

### IR
```
Summarize { input: "$0", prompt: Some("Summarize in 3 bullets.") }
```

---

# 7. Error Conditions

The IR compiler must produce fatal errors for:

### 7.1 Invalid reference
```
input: "$99"
```
when fewer than 100 operations exist.

### 7.2 Future reference
```
Summarize { input: "$2" }
```
when compiling IR[1].

### 7.3 Missing input
Should never occur if the normalizer is correct; if it does, error.

### 7.4 Unsupported AST node
If the normalizer emits a node not defined in this spec.

---

# 8. Non‑Goals

The IR does **not**:

- perform evaluation  
- construct prompts  
- call the model adapter  
- validate file paths  
- perform semantic analysis  

These responsibilities belong to the evaluator.

---

# Summary

The IR is a deterministic, linear execution plan with explicit references and no ambiguity.  
It is produced from the normalized AST and consumed by the evaluator.  
All IR nodes must follow the structures and rules defined in this document.