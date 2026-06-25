# <SPEC NAME>

## Purpose
Describe the purpose of this specification.  
Explain what part of the system it governs and why it exists.  
This section must clearly define the boundaries of the feature and what problem it solves.

This document must state whether the feature affects:
- the CNL grammar  
- the raw AST (parser output)  
- the normalized AST (normalizer output)  
- the IR compiler  
- the evaluator  
- the CLI  
- or any combination of these  

---

# 1. Overview
Provide a high‑level summary of the feature, including:

- What it does  
- Where it fits in the Limelight‑X pipeline  
- How it interacts with existing components  
- Whether it introduces or modifies:
  - grammar patterns  
  - raw AST node shapes  
  - normalization rules  
  - IR operations  
  - evaluator semantics  
  - CLI behavior  

This section should be readable by someone unfamiliar with the feature.

---

# 2. Requirements

## 2.1 Functional Requirements
List the behaviors the feature must support.

Examples:
- Must parse new CNL pattern  
- Must produce a specific raw AST structure  
- Must normalize pronouns, names, or implicit inputs  
- Must compile into IR nodes  
- Must evaluate deterministically  
- Must appear correctly in trace mode  

## 2.2 Non‑Functional Requirements
List constraints such as:

- determinism  
- error handling rules  
- safety considerations  
- performance expectations  
- compatibility with existing components  

---

# 3. Grammar (If Applicable)
Define any new CNL constructs introduced by this feature.

Include:

- sentence patterns  
- examples  
- pronoun rules  
- expression hole usage  
- constraints  
- how the grammar interacts with existing patterns  

Use the same style as `spec/cnl-grammar.md`.

---

# 4. Raw AST Specification (If Applicable)
Define the **raw AST node(s)** produced by the parser.

Include:

- node name  
- fields  
- types  
- invariants  
- examples  

Example format:
```
RawNodeName {
    field1: RawType,
    field2: RawType,
}
```

---

# 5. Normalized AST Specification (If Applicable)
Define how the AST Normalizer transforms the raw AST into a canonical form.

Include:

- normalization rules  
- pronoun resolution  
- variable binding  
- implicit input resolution  
- symbol table behavior  
- error conditions  

Example format:
```
NormalizedNodeName {
    field1: InputRef,
    field2: ExpressionRef,
}
```

---

# 6. IR Specification (If Applicable)
Define the IR node(s) introduced or modified by this feature.

Include:

- IR operation name  
- fields  
- allowed values  
- reference rules  
- examples  

Example format:
```
IR::NewOp {
    input: IRRef,
    config: String,
}
```

---

# 7. Evaluator Semantics (If Applicable)
Define how the evaluator must execute the new IR node(s).

Include:

- step‑by‑step behavior  
- prompt construction rules  
- deterministic vs nondeterministic behavior  
- error conditions  
- output type  

Example:
1. Resolve input reference  
2. Construct prompt  
3. Call model adapter  
4. Store result  

---

# 8. CLI Behavior (If Applicable)
Define how the CLI should expose this feature.

Include:

- new commands  
- new flags  
- examples  
- error messages  
- how the feature appears in:
  - `llx explain`  
  - `llx trace`  

---

# 9. Examples
Provide canonical examples showing:

- CNL input  
- raw AST output  
- normalized AST output  
- IR output  
- evaluator behavior  
- final output  

These examples serve as reference tests.

---

# 10. Error Conditions
List all error cases the feature must detect and report.

Examples:
- invalid grammar  
- missing fields  
- unsupported values  
- illegal IR references  
- evaluator failures  
- normalization failures (e.g., unresolvable pronouns)  

Each error must include a clear, human‑readable message.

---

# 11. BDD Scenarios
Define the acceptance criteria using the extended BDD format:

- GIVEN  
- WHEN  
- THEN  
- SO THAT  
- AS MEASURED BY  

These scenarios must be implemented as automated tests.

---

# 12. Non‑Goals
List what this feature does **not** attempt to solve.

Examples:
- does not introduce new IR nodes  
- does not modify evaluator semantics  
- does not support nested expressions  
- does not change CLI behavior  

This prevents scope creep.

---

# 13. Future Extensions
List ideas that are explicitly out of scope for this version but may be added later.

Examples:
- additional grammar patterns  
- richer evaluator behavior  
- multi‑step transformations  
- new IR operations  

---

# Summary
Summarize the feature in 2–3 sentences.  
Reaffirm the boundaries and the intended behavior.  
This section should be readable without referencing the rest of the document.