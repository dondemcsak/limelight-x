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

    resource: $ =>
      prec.right(repeat1(/[^.\n]+/)),

    target: $ =>
      prec.right(repeat1(/[^.\n]+/)),

    format_target: $ =>
      prec.right(repeat1(/[^.\n]+/)),

    language: $ =>
      prec.right(repeat1(/[^.\n]+/)),

    name: $ =>
      /[A-Za-z_][A-Za-z0-9_]*/,

    string: $ =>
      seq('"', repeat(/[^"]/), '"'),
  }
});