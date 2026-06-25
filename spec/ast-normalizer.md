# AST Normalizer

## Purpose
The AST Normalizer transforms the **raw AST** produced by the parser into a **canonical, fully explicit AST** suitable for IR compilation.

The normalizer is responsible for:
- resolving pronouns  
- resolving named variables  
- resolving implicit inputs  
- validating references  
- enforcing structural invariants  
- producing a normalized AST with **no ambiguity**

The IR compiler must never receive:
- pronouns  
- NamedVariable nodes  
- implicit inputs  
- Bind nodes  

The normalizer fully resolves all references before output.

---

# 1. Inputs and Outputs

## Input
A **raw AST** produced by the parser.  
This AST may contain:
- pronouns (`it`, `them`, `this`, `that`, `the result`, `the output`)
- named variables (`article`, `summary`, etc.)
- implicit inputs (e.g., `Extract the entities.`)
- Bind statements that introduce names

## Output
A **normalized AST** where:
- all inputs are explicit `InputRef` values  
- all names are resolved to their underlying `InputRef`  
- all pronouns are resolved to `PreviousResult`  
- all implicit inputs are resolved to `PreviousResult`  
- Bind statements are removed  
- **no NamedVariable nodes remain**  

This ensures the IR compiler receives only explicit, unambiguous references.

---

# 2. Symbol Table

The normalizer maintains a symbol table:

```
symbol_table: HashMap<String, InputRef>
```

Entries are added by `Bind` statements:

```
Let summary be the result.
→ symbol_table["summary"] = PreviousResult
```

The symbol table is used **only during normalization**.  
It is **not** passed to the IR compiler.

---

# 3. Normalization Rules

Normalization proceeds in a single pass over the AST.

For each AST node:

---

## 3.1 Load

```
Load { resource, path }
```

No changes required.  
Produces a new result index.

---

## 3.2 Extract

### Raw AST
```
Extract { target, input }
```

### Normalization
1. Resolve `input`:
   - pronoun → `PreviousResult`
   - name → lookup in symbol table → underlying `InputRef`
   - resource → `Resource(resource)`
2. If `input` is **missing** (implicit input):
   - replace with `PreviousResult`

### Example
```
Extract the entities.
→ Extract { target: "entities", input: PreviousResult }
```

---

## 3.3 Summarize

### Raw AST
```
Summarize { input, prompt }
```

### Normalization
Same rules as Extract:
- pronoun → `PreviousResult`
- name → symbol table lookup → underlying `InputRef`
- resource → `Resource(resource)`
- missing input → `PreviousResult`

Prompt is left unchanged.

---

## 3.4 Translate

Same normalization rules as Summarize.

---

## 3.5 Rewrite

Same normalization rules as Summarize.

---

## 3.6 Format

Same normalization rules as Summarize.

---

## 3.7 Bind

### Raw AST
```
Bind { name, value }
```

### Normalization
1. Resolve `value`:
   - pronoun → `PreviousResult`
   - name → symbol table lookup → underlying `InputRef`
2. Insert into symbol table:

```
symbol_table[name] = resolved_value
```

3. **Bind does not produce a normalized AST node.**  
   It is removed from the output stream.

---

# 4. InputRef Resolution

The normalizer must convert all inputs into one of:

```
InputRef::PreviousResult
InputRef::Resource(String)
```

### 4.1 Pronouns
```
it, them, this, that, the result, the output
→ PreviousResult
```

### 4.2 Named variables
Resolved immediately:

```
summary → symbol_table["summary"] → PreviousResult (or Resource)
```

### 4.3 Resources
Multi‑word noun phrases parsed as resources remain:

```
Resource("the article")
```

---

# 5. Error Conditions

The normalizer must produce fatal errors for:

### 5.1 Unresolvable pronoun
```
Summarize it.
```
with no previous result.

### 5.2 Unknown variable name
```
Summarize summary.
```
when `summary` has not been bound.

### 5.3 Bind to unsupported expression
Only pronouns and names are allowed:

```
Let x be Summarize the article.   // invalid
```

### 5.4 Missing previous result for implicit input
```
Extract the entities.
```
as the first statement.

### 5.5 Shadowed bindings
```
Let x be the result.
Let x be the result.   // shadowing not allowed
```

---

# 6. Output Format

The normalizer outputs a **vector of normalized AST nodes**, excluding Bind statements.

Example:

Raw AST:
```
[
  Load { ... },
  Extract { target: "entities", input: None },
  Summarize { input: Pronoun("them"), prompt: None }
]
```

Normalized AST:
```
[
  Load { ... },
  Extract { target: "entities", input: PreviousResult },
  Summarize { input: PreviousResult, prompt: None }
]
```

---

# 7. Non‑Goals

The normalizer does **not**:
- perform IR construction  
- validate prompt content  
- inspect file paths  
- call the model  
- perform semantic analysis beyond reference resolution  

---

# Summary

The AST Normalizer resolves all ambiguity in the raw AST:
- pronouns → explicit references  
- names → resolved to underlying InputRef  
- implicit inputs → previous result  
- Bind → symbol table only  

The output is a fully explicit, canonical AST ready for IR compilation, with **no NamedVariable nodes**.