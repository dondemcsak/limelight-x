# CNL Grammar

## Purpose
This document defines the **Constrained Natural Language (CNL)** syntax used by Limelight‑X.  
The grammar is intentionally small, deterministic, and unambiguous.  
It enables natural‑language‑like expression while remaining fully parseable into an AST.

This grammar is the authoritative contract for the parser.

---

# 1. General Rules

1. Each instruction is a **single sentence** ending with a period (`.`).
2. Each sentence represents **one action**.
3. Sentences must follow one of the supported patterns defined below.
4. Quoted strings (`"..."`) represent literal values.
5. Expression holes use the syntax:

   ```
   {{ prompt: "..." }}
   ```

6. Pronouns such as **it**, **them**, **this**, **that**, **the result**, **the output** refer to the output of the previous step.
7. Variables may be introduced using the `Let X be ...` pattern.

---

# 2. Supported Sentence Patterns

The following patterns define the full CNL surface area for v0.1.

Each pattern maps directly to an AST node type.

---

## 2.1 Load statements

### Pattern A
```
Load the <resource> from "<path>".
```

Examples:
- `Load the article from "article.txt".`
- `Load the text from "notes.md".`

### AST mapping
```
Load {
    resource: <resource>,
    path: <path>
}
```

---

## 2.2 Extraction statements

### Pattern B
```
Extract the <target> from <input>.
```

Examples:
- `Extract the entities from the article.`
- `Extract the key points from it.`

### Pattern C (implicit input)
```
Extract the <target>.
```

Examples:
- `Extract the entities.`
- `Extract the main ideas.`

### AST mapping
```
Extract {
    target: <target>,
    input: <reference or implicit previous result>
}
```

---

## 2.3 Summarization statements

### Pattern D
```
Summarize <input>.
```

(Pronouns and noun phrases are allowed directly, e.g., `Summarize it.` or `Summarize the article.`)

### Pattern E (with expression hole)
```
Summarize <input> using {{ prompt: "<prompt>" }}.
```

Examples:
- `Summarize the article.`
- `Summarize the ideas using {{ prompt: "Summarize in 3 bullets." }}.`
- `Summarize it using {{ prompt: "Summarize in 3 bullets." }}.`

### AST mapping
```
Summarize {
    input: <input>,
    prompt: Optional<String>
}
```

---

## 2.4 Translation statements

### Pattern F
```
Translate <input> to <language>.
```

### Pattern G (with expression hole)
```
Translate <input> to <language> using {{ prompt: "<prompt>" }}.
```

Examples:
- `Translate the article to French.`
- `Translate it to Spanish using {{ prompt: "Translate clearly and concisely." }}.`

### AST mapping
```
Translate {
    input: <input>,
    language: <language>,
    prompt: Optional<String>
}
```

---

## 2.5 Variable binding

### Pattern H
```
Let <name> be the <resource> from "<path>".
```

### Pattern I
```
Let <name> be <expression>.
```

Examples:
- `Let article be the text from "article.txt".`
- `Let summary be the result.`

### AST mapping
```
Bind {
    name: <name>,
    value: <expression>
}
```

---

## 2.6 Rewrite statements

### Pattern J
```
Rewrite <input>.
```

### Pattern K (with expression hole)
```
Rewrite <input> using {{ prompt: "<prompt>" }}.
```

Examples:
- `Rewrite the summary.`
- `Rewrite the summary using {{ prompt: "Rewrite in a friendly, conversational tone suitable for a Slack update." }}.`

### AST mapping
```
Rewrite {
    input: <input>,
    prompt: Optional<String>
}
```

---

## 2.7 Format statements

### Pattern L
```
Format <input> as <format-target>.
```

Examples:
- `Format the action items as JSON.`
- `Format the summary as a bullet list.`

### AST mapping
```
Format {
    input: <input>,
    target: <format-target>
}
```

---

# 3. Pronoun resolution rules

The following pronouns refer to the **output of the previous step**:

- `it`
- `them`
- `the result`
- `the output`
- `this`
- `that`

Example:
```
Extract the entities from the article.
Summarize them.
```

Pronoun resolution is performed by the **AST Normalizer**, not the parser.

---

# 4. Expression holes

Expression holes allow embedding a literal model prompt.

### Syntax
```
{{ prompt: "<string>" }}
```

### Rules
1. The prompt must be a quoted string.
2. The hole must appear after the keyword `using`.
3. The hole maps to the `prompt` field of the AST node.
4. The evaluator uses the prompt verbatim as the `<PROMPT>` portion of the template and still appends the input text.

---

# 5. Grammar summary (EBNF‑like)

```ebnf
Program        ::= Sentence+
Sentence       ::= LoadStmt
                 | ExtractStmt
                 | SummarizeStmt
                 | TranslateStmt
                 | BindStmt
                 | RewriteStmt
                 | FormatStmt

LoadStmt       ::= "Load the" Resource "from" String "."
ExtractStmt    ::= "Extract the" Target ("from" Input)? "."
SummarizeStmt  ::= "Summarize" Input (UsingPrompt)? "."
TranslateStmt  ::= "Translate" Input "to" Language (UsingPrompt)? "."
BindStmt       ::= "Let" Name "be" (ResourceFrom | Expression) "."
RewriteStmt    ::= "Rewrite" Input (UsingPrompt)? "."
FormatStmt     ::= "Format" Input "as" FormatTarget "."

UsingPrompt    ::= "using" PromptHole
PromptHole     ::= "{{ prompt:" String "}}"

Input          ::= Resource
                 | Name
                 | Pronoun

Pronoun        ::= "it"
                 | "them"
                 | "the result"
                 | "the output"
                 | "this"
                 | "that"

Expression     ::= Pronoun | Name

ResourceFrom   ::= Resource "from" String

Resource       ::= <multi-word noun phrase>
FormatTarget   ::= <free-text string>

Name           ::= <identifier: letters, digits, underscores>

String         ::= "\"" <characters> "\""

Target         ::= <multi-word noun phrase>
Language       ::= <language name>
```

---

# 6. Error conditions

The parser must produce clear errors for:

- unknown verbs  
- unsupported sentence patterns  
- missing periods  
- malformed expression holes  
- unresolvable pronouns  
- missing quoted strings  
- invalid variable names  

---

# 7. Non‑Goals

The grammar does **not** support:

- arbitrary natural language  
- nested clauses  
- conditionals  
- loops  
- multi‑sentence paragraphs  
- implicit file paths  
- multi‑prompt templates  

These may be added in future versions.

---

# Summary
This grammar defines the complete CNL surface area for Limelight‑X v0.1.  
All parsing, AST construction, and IR compilation must adhere strictly to this specification.
