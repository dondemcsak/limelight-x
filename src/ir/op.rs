/// A reference to the output of a previous IR operation by index ($0, $1, …).
#[derive(Debug, Clone, PartialEq)]
pub struct IrRef(pub usize);

/// A single IR operation.
#[derive(Debug, Clone, PartialEq)]
pub enum IrOp {
    Load {
        path: String,
    },
    Extract {
        target: String,
        input: IrRef,
    },
    Summarize {
        input: IrRef,
        prompt: Option<String>,
    },
    Translate {
        input: IrRef,
        language: String,
        prompt: Option<String>,
    },
    Rewrite {
        input: IrRef,
        prompt: Option<String>,
    },
    Format {
        input: IrRef,
        target: String,
    },
}

/// The complete linear IR program.
#[derive(Debug, Clone, PartialEq)]
pub struct Ir(pub Vec<IrOp>);

// ---------------------------------------------------------------------------
// Display implementations for explain / trace output
// ---------------------------------------------------------------------------

impl std::fmt::Display for IrRef {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "${}", self.0)
    }
}

impl std::fmt::Display for IrOp {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            IrOp::Load { path } => write!(f, "Load {{ path: \"{path}\" }}"),
            IrOp::Extract { target, input } => {
                write!(f, "Extract {{ target: \"{target}\", input: {input} }}")
            }
            IrOp::Summarize { input, prompt } => match prompt {
                Some(p) => write!(f, "Summarize {{ input: {input}, prompt: Some(\"{p}\") }}"),
                None => write!(f, "Summarize {{ input: {input}, prompt: None }}"),
            },
            IrOp::Translate {
                input,
                language,
                prompt,
            } => match prompt {
                Some(p) => write!(
                    f,
                    "Translate {{ input: {input}, language: \"{language}\", prompt: Some(\"{p}\") }}"
                ),
                None => write!(
                    f,
                    "Translate {{ input: {input}, language: \"{language}\", prompt: None }}"
                ),
            },
            IrOp::Rewrite { input, prompt } => match prompt {
                Some(p) => write!(f, "Rewrite {{ input: {input}, prompt: Some(\"{p}\") }}"),
                None => write!(f, "Rewrite {{ input: {input}, prompt: None }}"),
            },
            IrOp::Format { input, target } => {
                write!(f, "Format {{ input: {input}, target: \"{target}\" }}")
            }
        }
    }
}

impl std::fmt::Display for Ir {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        writeln!(f, "[")?;
        for (i, op) in self.0.iter().enumerate() {
            write!(f, "  [{i}] {op}")?;
            if i + 1 < self.0.len() {
                writeln!(f, ",")?;
            } else {
                writeln!(f)?;
            }
        }
        write!(f, "]")
    }
}
