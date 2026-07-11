//! Wire-format DTOs for `/src/api`, matching `spec/ux/ui-data-contracts.md`
//! exactly. These are deliberately separate from the internal parser/
//! normalizer/IR types (`RawNode`, `NormalizedNode`, `IrOp`, ...) — the wire
//! schema is a generic tree/list shape that doesn't map 1:1 onto those enums,
//! so this module owns the conversion instead of deriving `Serialize` on the
//! internal types directly.

use std::collections::HashMap;

use serde::Serialize;
use serde_json::Value;

use crate::error::Error;
use crate::ir::op::{Ir, IrOp};
use crate::normalizer::ast::{InputRef, NormalizedAst, NormalizedNode};
use crate::parser::ast::{RawAst, RawInput, RawNode};

// ---------------------------------------------------------------------------
// Shared response envelope (spec/ux/ui-data-contracts.md §1)
// ---------------------------------------------------------------------------

#[derive(Serialize)]
pub struct Envelope {
    pub version: &'static str,
    pub success: bool,
    pub errors: Vec<ErrorObject>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<Value>,
}

impl Envelope {
    pub fn ok<T: Serialize>(data: T) -> Self {
        Self {
            version: "v1",
            success: true,
            errors: vec![],
            data: Some(serde_json::to_value(data).expect("DTO must be serializable")),
        }
    }

    pub fn err_one(err: ErrorObject) -> Self {
        Self {
            version: "v1",
            success: false,
            errors: vec![err],
            data: None,
        }
    }
}

// ---------------------------------------------------------------------------
// Streaming event envelope (spec/api.md §2.1, spec/ux/ui-data-contracts.md §1)
// ---------------------------------------------------------------------------

pub const EVENT_PIPELINE_STARTED: &str = "pipeline_started";
pub const EVENT_RAW_AST_GENERATED: &str = "raw_ast_generated";
pub const EVENT_NORMALIZED_AST_GENERATED: &str = "normalized_ast_generated";
pub const EVENT_IR_GENERATED: &str = "ir_generated";
pub const EVENT_PROMPT_GENERATED: &str = "prompt_generated";
pub const EVENT_MODEL_OUTPUT_GENERATED: &str = "model_output_generated";
pub const EVENT_FINAL_RESULT_READY: &str = "final_result_ready";
pub const EVENT_PIPELINE_FAILED: &str = "pipeline_failed";

/// A single streamed WebSocket event: the same envelope shape as [`Envelope`]
/// plus `event_type`/`correlation_id` (spec/api.md §2.1).
///
/// Generic over its own payload type `T` so a single `serde_json::to_writer`
/// call at the point of sending (see `ws.rs::ClientSender::send`) serializes
/// straight from the typed DTO to bytes — no intermediate `serde_json::Value`
/// tree, per spec/api.md §2.3.
#[derive(Serialize)]
pub struct Event<T: Serialize> {
    pub version: &'static str,
    pub success: bool,
    pub errors: Vec<ErrorObject>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<T>,
    pub event_type: &'static str,
    pub correlation_id: String,
}

impl<T: Serialize> Event<T> {
    pub fn ok(event_type: &'static str, correlation_id: String, data: T) -> Self {
        Self {
            version: "v1",
            success: true,
            errors: vec![],
            data: Some(data),
            event_type,
            correlation_id,
        }
    }
}

/// `started`/`failed` carry no per-event payload, so `T` is never actually
/// serialized (`skip_serializing_if` hides `data: None`) — `()` is just a
/// placeholder to satisfy the `Event<T>` type.
impl Event<()> {
    pub fn started(correlation_id: String) -> Self {
        Self {
            version: "v1",
            success: true,
            errors: vec![],
            data: None,
            event_type: EVENT_PIPELINE_STARTED,
            correlation_id,
        }
    }

    pub fn failed(correlation_id: String, err: ErrorObject) -> Self {
        Self {
            version: "v1",
            success: false,
            errors: vec![err],
            data: None,
            event_type: EVENT_PIPELINE_FAILED,
            correlation_id,
        }
    }
}

/// The immediate synchronous response to `POST /run|/explain|/trace`
/// (spec/api.md §2.1) — actual results arrive later as [`Event`]s.
#[derive(Serialize)]
pub struct AckResponse {
    pub accepted: bool,
    pub correlation_id: String,
}

#[derive(Serialize)]
pub struct ErrorObject {
    pub code: &'static str,
    pub category: &'static str,
    pub message: String,
    pub severity: &'static str,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub location: Option<ErrorLocation>,
}

#[derive(Serialize)]
pub struct ErrorLocation {
    pub line: usize,
    pub column: usize,
    pub span: Span,
}

#[derive(Serialize, Clone, Copy)]
pub struct Span {
    pub start: usize,
    pub end: usize,
}

/// Fixed request-level errors (spec/api.md §10) — not derived from `Error`,
/// since these occur before the pipeline ever runs.
pub fn malformed_request_error() -> ErrorObject {
    ErrorObject {
        code: "ERR_MALFORMED_REQUEST",
        category: "api",
        message: "Malformed request body".to_string(),
        severity: "error",
        location: None,
    }
}

pub fn missing_field_error() -> ErrorObject {
    ErrorObject {
        code: "ERR_MISSING_FIELD",
        category: "api",
        message: "Missing required field 'source'".to_string(),
        severity: "error",
        location: None,
    }
}

/// Maps an internal pipeline `Error` onto the wire `code`/`category`/`severity`
/// taxonomy defined in `spec/api.md` §10. This is the single authoritative
/// mapping — every `Error` variant is classified by error *class*, not by
/// individual variant, so e.g. all four `ModelAdapter*` variants share one
/// code and the distinguishing detail lives in `message` (via `Error`'s
/// existing `Display`/`thiserror` impl, reused verbatim).
pub fn map_error(e: &Error) -> ErrorObject {
    let (code, category, severity) = classify(e);
    let location = match e {
        Error::ParseError { line, column, .. } => Some(ErrorLocation {
            line: *line,
            column: *column,
            // The parser does not track byte spans today; left at (0, 0).
            span: Span { start: 0, end: 0 },
        }),
        _ => None,
    };
    ErrorObject {
        code,
        category,
        message: e.to_string(),
        severity,
        location,
    }
}

fn classify(e: &Error) -> (&'static str, &'static str, &'static str) {
    match e {
        Error::ParseError { .. } => ("ERR_CNL_PARSE", "pipeline", "error"),
        Error::NormalizeError(_) => ("ERR_CNL_NORMALIZE", "pipeline", "error"),
        // Not explicitly enumerated in spec/api.md §10's table (which predates
        // IR-compiler-specific errors) — extends it non-breakingly, consistent
        // with ui-data-contracts.md §7's "backend may add new fields" allowance.
        Error::IrError(_) => ("ERR_IR_COMPILE", "pipeline", "error"),
        // The evaluator wraps model-adapter failures into `EvalError` (see
        // `evaluator::call_model`), re-stringifying the original variant —
        // so `Error::ModelAdapter*` below is never actually reachable via
        // the evaluate() path in practice. Recover the distinction from the
        // message text, which is `Error`'s own thiserror `Display` output
        // for those variants, always prefixed "model adapter " verbatim.
        Error::EvalError { message, .. } if message.contains("model adapter ") => {
            ("ERR_MODEL_ADAPTER", "pipeline", "fatal")
        }
        Error::EvalError { .. } => ("ERR_EVALUATOR_FATAL", "pipeline", "fatal"),
        Error::ModelAdapterNetworkError(_)
        | Error::ModelAdapterInvalidResponse(_)
        | Error::ModelAdapterMalformedResponse(_)
        | Error::ModelAdapterHttpError(_, _) => ("ERR_MODEL_ADAPTER", "pipeline", "fatal"),
        Error::MissingApiKey => ("ERR_MODEL_ADAPTER", "pipeline", "fatal"),
        Error::IoError(_) => ("ERR_EVALUATOR_FATAL", "pipeline", "fatal"),
    }
}

// ---------------------------------------------------------------------------
// AST node (spec/ux/ui-data-contracts.md §5.1)
// ---------------------------------------------------------------------------

#[derive(Serialize)]
pub struct AstNode {
    #[serde(rename = "type")]
    pub node_type: String,
    pub value: String,
    pub children: Vec<AstNode>,
    pub span: Span,
    pub depth: usize,
    pub metadata: AstNodeMetadata,
}

#[derive(Serialize)]
pub struct AstNodeMetadata {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub resource: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub pronoun: Option<String>,
    pub expression_hole: bool,
    pub normalized: bool,
}

#[derive(Serialize)]
pub struct AstMetadata {
    pub node_count: usize,
    pub max_depth: usize,
    pub source_length: usize,
}

#[derive(Serialize)]
pub struct NormalizedAstMetadata {
    pub node_count: usize,
    pub max_depth: usize,
    pub normalization_steps: usize,
    pub removed_named_variables: usize,
    pub added_input_refs: usize,
}

#[derive(Serialize)]
pub struct RawAstResponse {
    pub root: AstNode,
    pub raw_text: String,
    pub metadata: AstMetadata,
}

#[derive(Serialize)]
pub struct NormalizedAstResponse {
    pub root: AstNode,
    pub raw_text: String,
    pub metadata: NormalizedAstMetadata,
}

fn raw_node_type(node: &RawNode) -> &'static str {
    match node {
        RawNode::Load { .. } => "Load",
        RawNode::Extract { .. } => "Extract",
        RawNode::Summarize { .. } => "Summarize",
        RawNode::Translate { .. } => "Translate",
        RawNode::Rewrite { .. } => "Rewrite",
        RawNode::Format { .. } => "Format",
        RawNode::Bind { .. } => "Bind",
        RawNode::BindLoad { .. } => "BindLoad",
    }
}

fn raw_input_resource(input: &RawInput) -> Option<String> {
    match input {
        RawInput::Resource(r) => Some(r.clone()),
        _ => None,
    }
}

fn raw_input_pronoun(input: &RawInput) -> Option<String> {
    match input {
        RawInput::Pronoun(p) => Some(p.clone()),
        _ => None,
    }
}

fn raw_node_to_ast_node(node: &RawNode) -> AstNode {
    let (resource, pronoun, expression_hole) = match node {
        RawNode::Load { resource, .. } => (Some(resource.clone()), None, false),
        RawNode::Extract { input, .. } => match input {
            Some(i) => (raw_input_resource(i), raw_input_pronoun(i), false),
            None => (None, None, false),
        },
        RawNode::Summarize { input, prompt } => (
            raw_input_resource(input),
            raw_input_pronoun(input),
            prompt.is_some(),
        ),
        RawNode::Translate { input, prompt, .. } => (
            raw_input_resource(input),
            raw_input_pronoun(input),
            prompt.is_some(),
        ),
        RawNode::Rewrite { input, prompt } => (
            raw_input_resource(input),
            raw_input_pronoun(input),
            prompt.is_some(),
        ),
        RawNode::Format { input, .. } => {
            (raw_input_resource(input), raw_input_pronoun(input), false)
        }
        RawNode::Bind { .. } => (None, None, false),
        RawNode::BindLoad { resource, .. } => (Some(resource.clone()), None, false),
    };

    AstNode {
        node_type: raw_node_type(node).to_string(),
        value: format!("{node:?}"),
        children: vec![],
        span: Span { start: 0, end: 0 },
        depth: 1,
        metadata: AstNodeMetadata {
            resource,
            pronoun,
            expression_hole,
            normalized: false,
        },
    }
}

/// Builds the `/explain`/`/trace` `raw_ast` response.
///
/// The wire schema is a single-rooted tree; the pipeline's `RawAst` is a flat
/// statement list, so statements become depth-1 children of a synthetic
/// `Program` root. v0.1 does not decompose further into per-field sub-nodes.
pub fn raw_ast_response(raw: &RawAst, source: &str) -> RawAstResponse {
    let children: Vec<AstNode> = raw.0.iter().map(raw_node_to_ast_node).collect();
    let node_count = children.len() + 1; // +1 for the synthetic root
    let max_depth = if raw.0.is_empty() { 0 } else { 1 };
    let root = AstNode {
        node_type: "Program".to_string(),
        value: String::new(),
        children,
        span: Span { start: 0, end: 0 },
        depth: 0,
        metadata: AstNodeMetadata {
            resource: None,
            pronoun: None,
            expression_hole: false,
            normalized: false,
        },
    };
    RawAstResponse {
        root,
        raw_text: format!("{raw:#?}"),
        metadata: AstMetadata {
            node_count,
            max_depth,
            source_length: source.len(),
        },
    }
}

fn normalized_node_type(node: &NormalizedNode) -> &'static str {
    match node {
        NormalizedNode::Load { .. } => "Load",
        NormalizedNode::Extract { .. } => "Extract",
        NormalizedNode::Summarize { .. } => "Summarize",
        NormalizedNode::Translate { .. } => "Translate",
        NormalizedNode::Rewrite { .. } => "Rewrite",
        NormalizedNode::Format { .. } => "Format",
    }
}

fn normalized_node_input_ref(node: &NormalizedNode) -> Option<&InputRef> {
    match node {
        NormalizedNode::Load { .. } => None,
        NormalizedNode::Extract { input, .. } => Some(input),
        NormalizedNode::Summarize { input, .. } => Some(input),
        NormalizedNode::Translate { input, .. } => Some(input),
        NormalizedNode::Rewrite { input, .. } => Some(input),
        NormalizedNode::Format { input, .. } => Some(input),
    }
}

fn input_ref_resource(input: &InputRef) -> Option<String> {
    match input {
        InputRef::Resource(r) => Some(r.clone()),
        InputRef::PreviousResult => None,
    }
}

fn normalized_node_to_ast_node(node: &NormalizedNode) -> AstNode {
    let resource = normalized_node_input_ref(node).and_then(input_ref_resource);
    let expression_hole = matches!(
        node,
        NormalizedNode::Summarize {
            prompt: Some(_),
            ..
        } | NormalizedNode::Translate {
            prompt: Some(_),
            ..
        } | NormalizedNode::Rewrite {
            prompt: Some(_),
            ..
        }
    );

    AstNode {
        node_type: normalized_node_type(node).to_string(),
        value: format!("{node:?}"),
        children: vec![],
        span: Span { start: 0, end: 0 },
        depth: 1,
        metadata: AstNodeMetadata {
            resource,
            // The normalized AST never contains unresolved pronouns.
            pronoun: None,
            expression_hole,
            normalized: true,
        },
    }
}

/// Builds the `/explain`/`/trace` `normalized_ast` response. `raw` is used
/// only to compute `removed_named_variables` (counting `Bind`/`BindLoad`
/// nodes the normalizer removed) — the normalizer itself is untouched.
pub fn normalized_ast_response(norm: &NormalizedAst, raw: &RawAst) -> NormalizedAstResponse {
    let children: Vec<AstNode> = norm.0.iter().map(normalized_node_to_ast_node).collect();
    let node_count = children.len() + 1;
    let max_depth = if norm.0.is_empty() { 0 } else { 1 };

    let removed_named_variables = raw
        .0
        .iter()
        .filter(|n| matches!(n, RawNode::Bind { .. } | RawNode::BindLoad { .. }))
        .count();
    let added_input_refs = norm
        .0
        .iter()
        .filter(|n| matches!(normalized_node_input_ref(n), Some(InputRef::PreviousResult)))
        .count();

    let root = AstNode {
        node_type: "Program".to_string(),
        value: String::new(),
        children,
        span: Span { start: 0, end: 0 },
        depth: 0,
        metadata: AstNodeMetadata {
            resource: None,
            pronoun: None,
            expression_hole: false,
            normalized: true,
        },
    };

    NormalizedAstResponse {
        root,
        raw_text: format!("{norm:#?}"),
        metadata: NormalizedAstMetadata {
            node_count,
            max_depth,
            normalization_steps: removed_named_variables + added_input_refs,
            removed_named_variables,
            added_input_refs,
        },
    }
}

// ---------------------------------------------------------------------------
// IR (spec/ux/ui-data-contracts.md §5.4-5.5)
// ---------------------------------------------------------------------------

#[derive(Serialize)]
pub struct IrOperation {
    pub operation_index: usize,
    #[serde(rename = "type")]
    pub op_type: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub input: Option<usize>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub prompt: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub target: Option<String>,
    pub source_span: Span,
    pub normalized_source: String,
    pub debug_info: DebugInfo,
}

#[derive(Serialize)]
pub struct DebugInfo {
    pub token_count: usize,
    pub estimated_cost: f64,
}

#[derive(Serialize)]
pub struct IrMetadata {
    pub operation_count: usize,
    pub max_depth: usize,
    pub reference_map: HashMap<String, usize>,
}

#[derive(Serialize)]
pub struct IrResponse {
    pub operations: Vec<IrOperation>,
    pub raw_text: String,
    pub metadata: IrMetadata,
}

fn ir_op_type(op: &IrOp) -> &'static str {
    match op {
        IrOp::Load { .. } => "Load",
        IrOp::Extract { .. } => "Extract",
        IrOp::Summarize { .. } => "Summarize",
        IrOp::Translate { .. } => "Translate",
        IrOp::Rewrite { .. } => "Rewrite",
        IrOp::Format { .. } => "Format",
    }
}

fn ir_op_input(op: &IrOp) -> Option<usize> {
    match op {
        IrOp::Load { .. } => None,
        IrOp::Extract { input, .. } => Some(input.0),
        IrOp::Summarize { input, .. } => Some(input.0),
        IrOp::Translate { input, .. } => Some(input.0),
        IrOp::Rewrite { input, .. } => Some(input.0),
        IrOp::Format { input, .. } => Some(input.0),
    }
}

fn ir_op_prompt(op: &IrOp) -> Option<String> {
    match op {
        IrOp::Summarize { prompt, .. }
        | IrOp::Translate { prompt, .. }
        | IrOp::Rewrite { prompt, .. } => prompt.clone(),
        _ => None,
    }
}

fn ir_op_target(op: &IrOp) -> Option<String> {
    match op {
        IrOp::Extract { target, .. } | IrOp::Format { target, .. } => Some(target.clone()),
        _ => None,
    }
}

/// Builds the `/trace` `ir` response, reusing `IrOp`'s existing `Display`
/// impl (used today for `llx explain`'s text output) as `normalized_source`,
/// since no true source-text-span tracking exists yet.
pub fn ir_response(ir: &Ir) -> IrResponse {
    let operations =
        ir.0.iter()
            .enumerate()
            .map(|(i, op)| {
                let normalized_source = op.to_string();
                let token_count = approx_token_count(&normalized_source);
                IrOperation {
                    operation_index: i,
                    op_type: ir_op_type(op).to_string(),
                    input: ir_op_input(op),
                    prompt: ir_op_prompt(op),
                    target: ir_op_target(op),
                    source_span: Span { start: 0, end: 0 },
                    normalized_source,
                    debug_info: DebugInfo {
                        token_count,
                        // No real cost model wired up for v0.1.
                        estimated_cost: 0.0,
                    },
                }
            })
            .collect();

    let reference_map = (0..ir.0.len()).map(|i| (format!("${i}"), i)).collect();

    IrResponse {
        operations,
        raw_text: ir.to_string(),
        metadata: IrMetadata {
            operation_count: ir.0.len(),
            max_depth: if ir.0.is_empty() { 0 } else { 1 },
            reference_map,
        },
    }
}

// ---------------------------------------------------------------------------
// Prompts / model outputs (spec/ux/ui-data-contracts.md §5.6-5.7)
// ---------------------------------------------------------------------------

#[derive(Serialize)]
pub struct PromptBlockMetadata {
    pub length: usize,
    pub token_count: usize,
}

#[derive(Serialize)]
pub struct PromptBlock {
    pub operation_index: usize,
    pub prompt_text: String,
    pub metadata: PromptBlockMetadata,
}

pub fn prompt_block(operation_index: usize, prompt_text: &str) -> PromptBlock {
    PromptBlock {
        operation_index,
        prompt_text: prompt_text.to_string(),
        metadata: PromptBlockMetadata {
            length: prompt_text.chars().count(),
            token_count: approx_token_count(prompt_text),
        },
    }
}

#[derive(Serialize)]
pub struct ParsedContent {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub markdown: Option<Value>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub json: Option<Value>,
}

#[derive(Serialize)]
pub struct ModelOutputMetadata {
    pub token_usage: usize,
    pub latency_ms: u128,
}

#[derive(Serialize)]
pub struct ModelOutputBlock {
    pub operation_index: usize,
    pub raw_text: String,
    pub content_type: &'static str,
    pub parsed: ParsedContent,
    pub metadata: ModelOutputMetadata,
}

pub fn model_output_block(
    operation_index: usize,
    raw_text: &str,
    latency_ms: u128,
) -> ModelOutputBlock {
    let content_type = detect_content_type(raw_text);
    let json = if content_type == "json" {
        serde_json::from_str(raw_text).ok()
    } else {
        None
    };
    ModelOutputBlock {
        operation_index,
        raw_text: raw_text.to_string(),
        content_type,
        // Markdown-to-object parsing is deferred — no markdown
        // parser dependency is approved for v0.1.
        parsed: ParsedContent {
            markdown: None,
            json,
        },
        metadata: ModelOutputMetadata {
            token_usage: approx_token_count(raw_text),
            latency_ms,
        },
    }
}

// ---------------------------------------------------------------------------
// Final result (spec/ux/ui-data-contracts.md §5.8)
// ---------------------------------------------------------------------------

#[derive(Serialize)]
pub struct FinalResult {
    pub text: String,
    pub content_type: &'static str,
}

pub fn final_result(text: &str) -> FinalResult {
    FinalResult {
        text: text.to_string(),
        content_type: detect_content_type(text),
    }
}

// ---------------------------------------------------------------------------
// Per-event data payloads. Each streamed event carries only its own stage's
// data (unlike the old single-response `ExplainData`/`TraceData` which
// bundled every stage into one object) — one field each, keeping the same
// key names the old bundled DTOs used, so existing field-name expectations
// (and API consumers) carry over unchanged.
// ---------------------------------------------------------------------------

#[derive(Serialize)]
pub struct RawAstEventData {
    pub raw_ast: RawAstResponse,
}

#[derive(Serialize)]
pub struct NormalizedAstEventData {
    pub normalized_ast: NormalizedAstResponse,
}

#[derive(Serialize)]
pub struct IrEventData {
    pub ir: IrResponse,
}

#[derive(Serialize)]
pub struct PromptEventData {
    pub prompt: PromptBlock,
}

#[derive(Serialize)]
pub struct ModelOutputEventData {
    pub model_output: ModelOutputBlock,
}

/// Also used as `final_result_ready`'s event data for `/trace`.
#[derive(Serialize)]
pub struct RunData {
    pub final_result: FinalResult,
}

// ---------------------------------------------------------------------------
// Shared heuristics
// ---------------------------------------------------------------------------

fn detect_content_type(text: &str) -> &'static str {
    let trimmed = text.trim();
    let looks_like_json = (trimmed.starts_with('{') && trimmed.ends_with('}'))
        || (trimmed.starts_with('[') && trimmed.ends_with(']'));
    if looks_like_json && serde_json::from_str::<Value>(trimmed).is_ok() {
        return "json";
    }
    let markdown_markers = ["##", "**", "```", "\n- ", "\n* "];
    if markdown_markers.iter().any(|m| trimmed.contains(m)) {
        return "markdown";
    }
    "plain"
}

/// Approximates a token count as a whitespace-delimited word count. This is
/// a placeholder for v0.1 — no tokenizer dependency is approved (CLAUDE.md
/// §3.5); replace with an accurate count if one is added later.
fn approx_token_count(text: &str) -> usize {
    text.split_whitespace().count()
}
