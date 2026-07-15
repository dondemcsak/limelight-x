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
#
# tree-sitter/grammar.js's Resource/Target/FormatTarget/Language rules used
# to not implement the !KeywordWord guard below (plain repeat1(/[^.\n]+/),
# unbounded except by "." / newline) - a tracked, now-fixed Tree-sitter-only
# (client-side editor decoration) defect that never affected /src/parser's
# actual implementation of this rule. See spec/cnl-editor-architecture.md §5
# "Known Current Divergence" for the canonical pointer and
# spec/parsing/tree-sitter-runtime-build-guide.md §6 for the full empirical
# write-up, including the fix and its verification.
Resource       <- (!KeywordWord !"."
                    .)+

Target         <- Resource

FormatTarget   <- (!"." .)+

Language       <- (!"." .)+

# tree-sitter/grammar.js's Name rule used to have the same (default) lexer
# precedence as a resource word, which meant it truncated multi-word
# resources sharing its starting position (Input's choice(Resource, Name,
# Pronoun); Expression inside BindStmt's ResourceFrom-vs-Expression choice) -
# a second, related Tree-sitter-only defect, now fixed. See
# spec/cnl-editor-architecture.md §5's "Follow-Up" entry and
# spec/parsing/tree-sitter-runtime-build-guide.md §6's fifth finding.
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
