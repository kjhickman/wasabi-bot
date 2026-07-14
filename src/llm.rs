use serde::{Deserialize, Serialize};

const USE_MAGIC_CONCH_TOOL: &str = "UseMagicConch";
const GEMINI_GENERATE_CONTENT_URL: &str =
    "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent";
const CONCH_PROMPT: &str = "You are the Magic Conch shell. The user asks a yes/no style question and you reply succinctly. \
Rules: If the question is NOT yes/no, respond exactly with 'Try asking again'. \
If you confidently know, reply only 'Yes' or 'No'. \
If uncertain or ambiguous, respond exactly with 'UseMagicConch' (do not guess). \
Never add extra commentary, punctuation, or markdown.";

#[tracing::instrument(
    name = "llm.gemini.generate_content",
    skip(client, api_key, question),
    fields(model = "gemini-3.5-flash")
)]
pub async fn conch_answer(
    client: &reqwest::Client,
    api_key: &str,
    question: &str,
) -> anyhow::Result<Option<String>> {
    let prompt = format!("{CONCH_PROMPT}\nQuestion: {question}");

    let response = client
        .post(GEMINI_GENERATE_CONTENT_URL)
        .header("x-goog-api-key", api_key)
        .json(&GenerateContentRequest {
            contents: vec![Content {
                role: "user",
                parts: vec![Part { text: &prompt }],
            }],
            generation_config: GenerationConfig {
                temperature: 0.2,
                max_output_tokens: 8,
            },
        })
        .send()
        .await?
        .error_for_status()?
        .json::<GenerateContentResponse>()
        .await?;

    let text = response
        .candidates
        .into_iter()
        .next()
        .and_then(|candidate| candidate.content.parts.into_iter().next())
        .map(|part| part.text)
        .unwrap_or_default();

    Ok(interpret_conch_answer(&text))
}

pub fn interpret_conch_answer(text: &str) -> Option<String> {
    let answer = text.trim();
    if answer.is_empty() || answer.eq_ignore_ascii_case(USE_MAGIC_CONCH_TOOL) {
        None
    } else {
        Some(answer.to_string())
    }
}

#[derive(Serialize)]
struct GenerateContentRequest<'a> {
    contents: Vec<Content<'a>>,
    #[serde(rename = "generationConfig")]
    generation_config: GenerationConfig,
}

#[derive(Serialize)]
struct Content<'a> {
    role: &'a str,
    parts: Vec<Part<'a>>,
}

#[derive(Serialize, Deserialize)]
struct Part<'a> {
    text: &'a str,
}

#[derive(Serialize)]
struct GenerationConfig {
    temperature: f32,
    #[serde(rename = "maxOutputTokens")]
    max_output_tokens: u32,
}

#[derive(Deserialize)]
struct GenerateContentResponse {
    #[serde(default)]
    candidates: Vec<Candidate>,
}

#[derive(Deserialize)]
struct Candidate {
    content: ResponseContent,
}

#[derive(Deserialize)]
struct ResponseContent {
    #[serde(default)]
    parts: Vec<ResponsePart>,
}

#[derive(Deserialize)]
struct ResponsePart {
    text: String,
}

#[cfg(test)]
mod tests {
    use super::interpret_conch_answer;

    #[test]
    fn interpret_conch_answer_falls_back_on_tool_sentinel() {
        assert_eq!(interpret_conch_answer(" UseMagicConch\n"), None);
        assert_eq!(interpret_conch_answer(" \n"), None);
    }

    #[test]
    fn interpret_conch_answer_trims_direct_answer() {
        assert_eq!(interpret_conch_answer(" Yes\n"), Some("Yes".into()));
    }
}
