# ============================================================
# Limelight‑X PEG Grammar (v0.1)
# Deterministic, unambiguous, single‑sentence CNL grammar
# ============================================================

Program        <- Sentence+

Sentence       <- LoadStmt
                / ExtractStmt
                / SummarizeStmt
                / TranslateStmt
                / BindStmt
                / RewriteStmt
                / FormatStmt

# ------------------------------------------------------------
# Load Statements
# ------------------------------------------------------------
LoadStmt       <- "Load the" _ Resource _ "from" _ String "." _

# ------------------------------------------------------------
# Extraction Statements
# ------------------------------------------------------------
ExtractStmt    <- "Extract the" _ Target ( _ "from" _ Input )? "." _

# ------------------------------------------------------------
# Summarization Statements
# ------------------------------------------------------------
SummarizeStmt  <- "Summarize" _ Input ( _ UsingPrompt )? "." _

UsingPrompt    <- "using" _ PromptHole

PromptHole     <- "{{" _ "prompt:" _ String _ "}}" 

# ------------------------------------------------------------
# Translation Statements
# ------------------------------------------------------------
TranslateStmt  <- "Translate" _ Input _ "to" _ Language ( _ UsingPrompt )? "." _

# ------------------------------------------------------------
# Variable Binding
# ------------------------------------------------------------
BindStmt       <- "Let" _ Name _ "be" _ ( ResourceFrom / Expression ) "." _

ResourceFrom   <- Resource _ "from" _ String

Expression     <- Pronoun / Name

# ------------------------------------------------------------
# Rewrite Statements
# ------------------------------------------------------------
RewriteStmt    <- "Rewrite" _ Input ( _ UsingPrompt )? "." _

# ------------------------------------------------------------
# Format Statements
# ------------------------------------------------------------
FormatStmt     <- "Format" _ Input _ "as" _ FormatTarget "." _

# ------------------------------------------------------------
# Core Inputs
# ------------------------------------------------------------
Input          <- Resource
                / Name
                / Pronoun

# ------------------------------------------------------------
# Pronouns (previous-step references)
# ------------------------------------------------------------
Pronoun        <- "it"
                / "them"
                / "the result"
                / "the output"
                / "this"
                / "that"

# ------------------------------------------------------------
# Lexical Elements
# ------------------------------------------------------------

# Multi-word noun phrase: greedy until a keyword boundary
Resource       <- (!KeywordWord !"."
                    .)+

Target         <- Resource

FormatTarget   <- (!"." .)+

Language       <- (!"." .)+

Name           <- NameStart NameChar*

NameStart      <- [A-Za-z_]
NameChar       <- [A-Za-z0-9_]

String         <- "\"" StringChar* "\""
StringChar     <- ! "\"" .

# ------------------------------------------------------------
# Whitespace
# ------------------------------------------------------------
_              <- [ \t\r\n]*

# ------------------------------------------------------------
# Keyword Guard (prevents Resource from consuming keywords)
# ------------------------------------------------------------
KeywordWord    <- "from"
                / "using"
                / "to"
                / "as"
                / "be"
                / "Load"
                / "Extract"
                / "Summarize"
                / "Translate"
                / "Let"
                / "Rewrite"
                / "Format"
