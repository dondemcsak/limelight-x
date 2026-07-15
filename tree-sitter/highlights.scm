; Keywords
["Load the" "Extract the" "Summarize" "Translate" "Let" "Rewrite" "Format"
 "from" "using" "to" "as" "be"] @keyword

; Strings
(string) @string

; Identifiers (variable names, e.g. "Let summary be ...")
(name) @variable

; Free-text noun phrases (resource/target/format_target/language) - all
; render the same as (name) above, i.e. TokenKind.Resource
; (ui/components/TokenKind.cs) - the default class for any non-keyword/non-pronoun word.
(resource) @variable
(target) @variable
(format_target) @variable
(language) @variable

; Pronouns
(pronoun) @variable.builtin

; Prompt hole
(prompt_hole) @embedded
