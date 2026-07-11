```javascript
module.exports = grammar({
  name: "limelightx",

  extras: $ => [/\s+/],

  rules: {
    program: $ => repeat($.sentence),

    sentence: $ => choice(
      $.load_stmt,
      $.extract_stmt,
      $.summarize_stmt,
      $.translate_stmt,
      $.bind_stmt,
      $.rewrite_stmt,
      $.format_stmt
    ),

    load_stmt: $ =>
      seq("Load the", $.resource, "from", $.string, "."),

    extract_stmt: $ =>
      seq("Extract the", $.target, optional(seq("from", $.input)), "."),

    summarize_stmt: $ =>
      seq("Summarize", $.input, optional($.using_prompt), "."),

    translate_stmt: $ =>
      seq("Translate", $.input, "to", $.language, optional($.using_prompt), "."),

    bind_stmt: $ =>
      seq("Let", $.name, "be", choice($.resource_from, $.expression), "."),

    rewrite_stmt: $ =>
      seq("Rewrite", $.input, optional($.using_prompt), "."),

    format_stmt: $ =>
      seq("Format", $.input, "as", $.format_target, "."),

    using_prompt: $ =>
      seq("using", $.prompt_hole),

    prompt_hole: $ =>
      seq("{{", "prompt:", $.string, "}}"),

    resource_from: $ =>
      seq($.resource, "from", $.string),

    expression: $ =>
      choice($.pronoun, $.name),

    input: $ =>
      choice($.resource, $.name, $.pronoun),

    pronoun: $ =>
      choice(
        "it",
        "them",
        "the result",
        "the output",
        "this",
        "that"
      ),

    // Free-text noun phrases, tokenized word-by-word (not one greedy regex
    // spanning the whole run) specifically so each word is a separate lexer
    // decision point. _free_text_word's token(prec(-1, ...)) gives it LOWER
    // precedence than every plain string literal in this grammar (keyword
    // literals and "from"/"using"/"to"/"as"/"be" default to precedence 0) -
    // Tree-sitter's lexer conflict resolution checks precedence BEFORE match
    // length, so at the exact position where a keyword could start, the
    // keyword always wins over continuing as another free-text word,
    // regardless of which match would be longer. This restores the PEG
    // spec's !KeywordWord guard (spec/parsing/peg-grammar.md), which this
    // grammar previously had no equivalent for - see
    // spec/cnl-editor-architecture.md §5 "Known Current Divergence" and
    // spec/parsing/tree-sitter-runtime-build-guide.md §6 for the bug this
    // fixes and how it was diagnosed.
    resource: $ =>
      prec.right(repeat1($._free_text_word)),

    target: $ =>
      prec.right(repeat1($._free_text_word)),

    format_target: $ =>
      prec.right(repeat1($._free_text_word)),

    language: $ =>
      prec.right(repeat1($._free_text_word)),

    _free_text_word: $ =>
      token(prec(-1, /[^\s.\n]+/)),

    // Precedence -2: BELOW _free_text_word's -1, so at the two ambiguous
    // positions where $.name directly competes with $.resource for the same
    // starting word (expression's choice($.pronoun, $.name) inside
    // bind_stmt's resource_from-vs-expression choice, and input's
    // choice($.resource, $.name, $.pronoun)), the lexer now prefers
    // continuing as a free-text word over reducing to a standalone name.
    // Without this, a multi-word resource/target beginning with an
    // ordinary identifier-shaped word (e.g. "the" in "the article", "the
    // text") got truncated to that one word as `name`, leaving the rest of
    // the phrase as an unparsed ERROR - confirmed against both bind_stmt's
    // resource_from ("Let article be the text from \"article.txt\".") and
    // extract_stmt/summarize_stmt's input ("Extract the entities from the
    // article.", cnl-grammar.md §2.2's own example). $.name in bind_stmt's
    // unambiguous first slot ("Let" $.name "be") is unaffected - no
    // competing alternative exists there regardless of precedence.
    // Known accepted narrowing: a single bare name reference used as a
    // *complete* expression with nothing following (e.g. "Let summary be
    // article." - no "from", not a pronoun) now fails to parse, since the
    // lexer commits to a free-text-word token before the parser can know
    // resource_from will dead-end. No spec example anywhere uses this
    // exact shape; see spec/cnl-editor-architecture.md §5 for the tracking
    // note and the multi-word-truncation-vs-this-narrower-gap tradeoff.
    name: $ =>
      token(prec(-2, /[A-Za-z_][A-Za-z0-9_]*/)),

    // Content excludes newline (not just '"'), matching _free_text_word's
    // own /[^\s.\n]+/ boundary above. Without this, an unterminated string
    // (missing closing '"') has no bounded failure point for Tree-sitter's
    // GLR error recovery to insert a MISSING '"' at - the repeat regex
    // greedily consumes everything up to true EOF trying to find a closing
    // quote, so the whole remainder of the document becomes one ERROR node
    // instead of a clean, position-specific MISSING '"' (confirmed
    // empirically pre-fix: ui/tests/Intellisense/DiagnosticServiceTests.cs's
    // doc comment documents the prior unreachable-quote-case finding this
    // change addresses). Bounding content at the newline gives recovery a
    // concrete stopping point on any line but the last, the same way the
    // missing-period case already recovers cleanly at end-of-sentence. No
    // spec example anywhere embeds a literal newline inside a quoted string
    // (cnl-grammar.md, all worked examples) - this narrows nothing that was
    // ever actually usable.
    string: $ =>
      seq('"', repeat(/[^"\n]/), '"'),
  }
});
```
