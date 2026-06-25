# BDD Scenarios

## Purpose
This document defines the **Behavior‑Driven Development (BDD)** scenarios for Limelight‑X.  
These scenarios serve as the **acceptance criteria** for the system and must be implemented as automated tests.  
Claude must use these scenarios to validate correctness and drive iterative refinement.

Each scenario follows the extended BDD structure:

- **GIVEN** — initial context  
- **WHEN** — action taken  
- **THEN** — expected behavior  
- **SO THAT** — purpose or user value  
- **AS MEASURED BY** — objective, testable metric  

These scenarios are authoritative.  
Claude must not modify them unless explicitly instructed.

---

# 1. Parsing Scenarios

## Scenario: Parse a simple Load statement
**GIVEN** a file containing  
```
Load the article from "article.txt".
```  
**WHEN** the parser runs  
**THEN** it produces a raw AST with a single `Load` node  
**SO THAT** the system can compile the instruction  
**AS MEASURED BY** the AST matching the structure defined in `cnl-grammar.md`

---

## Scenario: Parse a Summarize statement with an expression hole
**GIVEN** a file containing  
```
Summarize the article using {{ prompt: "Summarize in 3 bullets." }}.
```  
**WHEN** the parser runs  
**THEN** it produces a `Summarize` AST node with a `prompt` field containing the exact string  
**SO THAT** the evaluator can pass the prompt verbatim to the model  
**AS MEASURED BY** the AST containing `prompt = Some("Summarize in 3 bullets.")`

---

## Scenario: Parse Rewrite and Format statements
**GIVEN** a file containing  
```
Rewrite the summary using {{ prompt: "Rewrite in a friendly tone." }}.
Format the summary as JSON.
```  
**WHEN** the parser runs  
**THEN** it produces `Rewrite` and `Format` AST nodes  
**SO THAT** the IR compiler can generate the correct operations  
**AS MEASURED BY** AST nodes matching the grammar definitions

---

## Scenario: Parse pronouns
**GIVEN** a file containing  
```
Load the article from "article.txt".
Summarize it.
```  
**WHEN** the parser runs  
**THEN** the second sentence contains a pronoun input  
**SO THAT** the normalizer can resolve it  
**AS MEASURED BY** the raw AST containing `input = Pronoun("it")`

---

# 2. AST Normalization Scenarios

## Scenario: Pronoun resolution
**GIVEN** a raw AST containing  
```
Load(...)
Summarize(input = Pronoun("it"))
```  
**WHEN** the normalizer runs  
**THEN** it resolves `it` to `PreviousResult`  
**SO THAT** the IR compiler receives explicit references  
**AS MEASURED BY** the normalized AST containing `input = PreviousResult`

---

## Scenario: Implicit input resolution
**GIVEN** a raw AST containing  
```
Load(...)
Extract(target="entities", input=None)
```  
**WHEN** the normalizer runs  
**THEN** it resolves the missing input to `PreviousResult`  
**SO THAT** implicit inputs behave predictably  
**AS MEASURED BY** the normalized AST containing `input = PreviousResult`

---

## Scenario: Variable binding resolution
**GIVEN** a raw AST containing  
```
Let summary be the result.
Summarize summary.
```  
**WHEN** the normalizer runs  
**THEN** it resolves `summary` to the underlying `InputRef` (e.g., `PreviousResult`)  
**SO THAT** named variables work as expected  
**AS MEASURED BY** the normalized AST containing `input = PreviousResult` and **no NamedVariable nodes**

---

## Scenario: Pronoun resolution failure
**GIVEN** a file containing  
```
Summarize it.
```  
**WHEN** the normalizer runs  
**THEN** it produces a fatal error  
**SO THAT** invalid programs fail early  
**AS MEASURED BY** an error message: “No prior result for pronoun ‘it’”

---

## Scenario: Unknown variable name
**GIVEN** a file containing  
```
Summarize summary.
```  
**WHEN** the normalizer runs  
**THEN** it produces a fatal error  
**SO THAT** invalid references fail early  
**AS MEASURED BY** an error: “Unknown variable ‘summary’”

---

# 3. IR Compilation Scenarios

## Scenario: Compile Load + Summarize
**GIVEN** a normalized AST with  
- `Load(path="article.txt")`  
- `Summarize(input=PreviousResult, prompt=None)`  
**WHEN** the IR compiler runs  
**THEN** it produces two IR nodes: `Load` then `Summarize`  
**SO THAT** evaluation is deterministic  
**AS MEASURED BY** the IR list matching the structure in `ir.md`

---

## Scenario: Compile Summarize with expression hole
**GIVEN** a normalized `Summarize` node with a custom prompt  
**WHEN** the IR compiler runs  
**THEN** it embeds the prompt verbatim in the IR  
**SO THAT** the evaluator can construct the correct model call  
**AS MEASURED BY** the IR containing `prompt = Some("<string>")`

---

## Scenario: Compile Extract + Summarize chain
**GIVEN** a normalized AST representing  
```
Load → Extract → Summarize
```  
**WHEN** the IR compiler runs  
**THEN** it produces a linear IR with correct `$0`, `$1`, `$2` references  
**SO THAT** evaluation order is explicit  
**AS MEASURED BY** the IR referencing only earlier operations

---

## Scenario: Compile Rewrite and Format
**GIVEN** a normalized AST containing  
```
Rewrite(input=PreviousResult, prompt=None)
Format(input=PreviousResult, target="JSON")
```  
**WHEN** the IR compiler runs  
**THEN** it produces `Rewrite` and `Format` IR nodes  
**SO THAT** the evaluator can execute them  
**AS MEASURED BY** IR nodes matching the definitions in `ir.md`

---

# 4. Evaluation Scenarios

## Scenario: Evaluate a Load operation
**GIVEN** an IR containing  
```
Load { path: "article.txt" }
```  
**WHEN** the evaluator runs  
**THEN** it reads the file and stores its contents  
**SO THAT** downstream operations have input  
**AS MEASURED BY** `results[0]` containing the file contents

---

## Scenario: Evaluate Summarize with built‑in prompt
**GIVEN** an IR containing  
```
Summarize { input: "$0", prompt: None }
```  
**WHEN** the evaluator runs  
**THEN** it constructs the built‑in summarization prompt  
**SO THAT** summarization is deterministic  
**AS MEASURED BY** the prompt matching the template in `evaluator-semantics.md`

---

## Scenario: Evaluate Summarize with expression hole
**GIVEN** an IR containing  
```
Summarize { input: "$0", prompt: Some("Summarize in 3 bullets.") }
```  
**WHEN** the evaluator runs  
**THEN** it uses the custom prompt verbatim and appends the input  
**SO THAT** user intent is preserved  
**AS MEASURED BY** the model adapter receiving the exact constructed prompt

---

## Scenario: Evaluate Translate
**GIVEN** an IR containing  
```
Translate { input: "$0", language: "French", prompt: None }
```  
**WHEN** the evaluator runs  
**THEN** it constructs the built‑in translation prompt  
**SO THAT** translation is consistent  
**AS MEASURED BY** the prompt matching the template in `evaluator-semantics.md`

---

## Scenario: Evaluate Rewrite
**GIVEN** an IR containing  
```
Rewrite { input: "$0", prompt: None }
```  
**WHEN** the evaluator runs  
**THEN** it constructs the built‑in rewrite prompt  
**SO THAT** rewriting is deterministic  
**AS MEASURED BY** the constructed prompt matching the template

---

## Scenario: Evaluate Format
**GIVEN** an IR containing  
```
Format { input: "$0", target: "JSON" }
```  
**WHEN** the evaluator runs  
**THEN** it constructs the built‑in formatting prompt  
**SO THAT** formatting is consistent  
**AS MEASURED BY** the constructed prompt matching the template

---

# 5. Trace Mode Scenarios

## Scenario: Trace mode shows AST, normalized AST, IR, prompts, and results
**GIVEN** a valid `.llx` file  
**WHEN** the user runs `llx trace file.llx`  
**THEN** the system prints  
- raw AST  
- normalized AST  
- IR  
- each IR operation  
- constructed prompts  
- model outputs  
- final result  
**SO THAT** users can understand the full execution pipeline  
**AS MEASURED BY** trace output containing all required sections

---

# 6. Explain Mode Scenarios

## Scenario: Explain mode shows AST, normalized AST, and IR without evaluating
**GIVEN** a valid `.llx` file  
**WHEN** the user runs `llx explain file.llx`  
**THEN** the system prints  
- raw AST  
- normalized AST  
- IR  
**SO THAT** users can inspect compilation without executing  
**AS MEASURED BY** explain output containing all three sections

---

# 7. Error Handling Scenarios

## Scenario: Missing file error
**GIVEN** an IR containing  
```
Load { path: "missing.txt" }
```  
**WHEN** the evaluator runs  
**THEN** it produces a fatal error  
**SO THAT** users receive clear feedback  
**AS MEASURED BY** an error containing the operation index and message

---

## Scenario: Invalid expression hole
**GIVEN** a CNL file with malformed prompt syntax  
**WHEN** the parser runs  
**THEN** it produces a syntax error  
**SO THAT** invalid programs fail early  
**AS MEASURED BY** an error referencing the exact line and column

---

# Summary
These BDD scenarios define the complete behavioral contract for Limelight‑X v0.1.  
All automated tests must map directly to these scenarios, and all implementation must satisfy them.