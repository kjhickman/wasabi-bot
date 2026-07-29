use base64::Engine;
use base64::engine::general_purpose::STANDARD;
use serde::{Deserialize, Serialize};

const USE_MAGIC_CONCH_TOOL: &str = "UseMagicConch";
const GEMINI_GENERATE_CONTENT_URL: &str =
    "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent";
const CONCH_PROMPT: &str = "You are the Magic Conch shell. The user asks a yes/no style question and you reply succinctly. \
Rules: If the question is NOT yes/no, respond exactly with 'Try asking again'. \
If you confidently know, reply only 'Yes' or 'No'. \
If uncertain or ambiguous, respond exactly with 'UseMagicConch' (do not guess). \
Never add extra commentary, punctuation, or markdown.";
const CAPTION_PROMPT: &str = "Look at this image and create a memey caption for it: \
Keep it concise but entertaining. Don't describe what you see, just provide the caption.";

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
                parts: vec![Part::Text { text: &prompt }],
            }],
            generation_config: GenerationConfig {
                temperature: 0.2,
                max_output_tokens: 8,
            },
        })
        .send()
        .await?
        .error_for_status_with_body()
        .await?
        .json::<GenerateContentResponse>()
        .await?;

    let text = response.text().unwrap_or_default();

    Ok(interpret_conch_answer(&text))
}

#[tracing::instrument(
    name = "llm.gemini.generate_content",
    skip(client, api_key, image_bytes),
    fields(model = "gemini-3.5-flash")
)]
pub async fn caption_answer(
    client: &reqwest::Client,
    api_key: &str,
    mime_type: &str,
    image_bytes: &[u8],
) -> anyhow::Result<String> {
    let image_data = STANDARD.encode(image_bytes);
    let response = client
        .post(GEMINI_GENERATE_CONTENT_URL)
        .header("x-goog-api-key", api_key)
        .json(&GenerateContentRequest {
            contents: vec![Content {
                role: "user",
                parts: vec![
                    Part::InlineData {
                        inline_data: InlineData {
                            mime_type,
                            data: &image_data,
                        },
                    },
                    Part::Text {
                        text: CAPTION_PROMPT,
                    },
                ],
            }],
            generation_config: GenerationConfig {
                temperature: 0.6,
                max_output_tokens: 1024,
            },
        })
        .send()
        .await?
        .error_for_status_with_body()
        .await?
        .json::<GenerateContentResponse>()
        .await?;

    response
        .text()
        .filter(|text| !text.is_empty())
        .ok_or_else(|| anyhow::anyhow!("Gemini returned an empty caption"))
}

#[must_use]
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

#[derive(Serialize)]
#[serde(untagged)]
enum Part<'a> {
    Text {
        text: &'a str,
    },
    InlineData {
        #[serde(rename = "inline_data")]
        inline_data: InlineData<'a>,
    },
}

#[derive(Serialize)]
struct InlineData<'a> {
    #[serde(rename = "mime_type")]
    mime_type: &'a str,
    data: &'a str,
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
    #[serde(default)]
    text: String,
}

impl GenerateContentResponse {
    fn text(&self) -> Option<String> {
        let text: String = self
            .candidates
            .first()
            .into_iter()
            .flat_map(|candidate| &candidate.content.parts)
            .map(|part| part.text.as_str())
            .collect::<Vec<_>>()
            .join("");

        let text = text.trim();
        (!text.is_empty()).then(|| text.to_string())
    }
}

trait ResponseExt {
    async fn error_for_status_with_body(self) -> anyhow::Result<Self>
    where
        Self: Sized;
}

impl ResponseExt for reqwest::Response {
    async fn error_for_status_with_body(self) -> anyhow::Result<Self> {
        let status = self.status();
        if status.is_success() {
            return Ok(self);
        }

        let url = self.url().clone();
        let body = self.text().await.unwrap_or_default();
        anyhow::bail!("Gemini request failed with {status} for {url}: {body}");
    }
}

#[cfg(test)]
mod tests {
    use super::interpret_conch_answer;
    use super::{Candidate, GenerateContentResponse, ResponseContent, ResponsePart};

    #[test]
    fn interpret_conch_answer_falls_back_on_tool_sentinel() {
        assert_eq!(interpret_conch_answer(" UseMagicConch\n"), None);
        assert_eq!(interpret_conch_answer(" \n"), None);
    }

    #[test]
    fn interpret_conch_answer_trims_direct_answer() {
        assert_eq!(interpret_conch_answer(" Yes\n"), Some("Yes".into()));
    }

    #[test]
    fn response_text_joins_all_text_parts() {
        let response = GenerateContentResponse {
            candidates: vec![Candidate {
                content: ResponseContent {
                    parts: vec![
                        ResponsePart {
                            text: "That tongue has".into(),
                        },
                        ResponsePart {
                            text: " entered its villain era".into(),
                        },
                    ],
                },
            }],
        };

        assert_eq!(
            response.text(),
            Some("That tongue has entered its villain era".into())
        );
    }
}
