use opentelemetry::trace::TracerProvider as _;
use tracing_subscriber::layer::SubscriberExt;
use tracing_subscriber::util::SubscriberInitExt;

/// Initializes tracing: always logs to stdout; additionally exports traces and
/// logs over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set (injected by Aspire).
/// Endpoint/headers/protocol are read from standard OTEL_* env vars by the exporter.
/// OTLP setup failures are non-fatal: stdout logging is always installed.
pub fn init() {
    let filter = tracing_subscriber::EnvFilter::try_from_default_env()
        .unwrap_or_else(|_| "info,serenity=warn".into());
    let registry = tracing_subscriber::registry()
        .with(filter)
        .with(tracing_subscriber::fmt::layer());

    let otel = if std::env::var("OTEL_EXPORTER_OTLP_ENDPOINT").is_ok() {
        match build_otel_providers() {
            Ok(providers) => Some(providers),
            Err(error) => {
                eprintln!("OTLP exporter setup failed, continuing without export: {error:?}");
                None
            }
        }
    } else {
        None
    };

    match otel {
        Some((tracer_provider, logger_provider)) => {
            let tracer = tracer_provider.tracer("wasabi-bot");
            registry
                .with(tracing_opentelemetry::layer().with_tracer(tracer))
                .with(
                    opentelemetry_appender_tracing::layer::OpenTelemetryTracingBridge::new(
                        &logger_provider,
                    ),
                )
                .init();
            opentelemetry::global::set_tracer_provider(tracer_provider);
        }
        None => registry.init(),
    }
}

fn build_otel_providers() -> anyhow::Result<(
    opentelemetry_sdk::trace::SdkTracerProvider,
    opentelemetry_sdk::logs::SdkLoggerProvider,
)> {
    let resource = opentelemetry_sdk::Resource::builder().build();

    let tracer_provider = opentelemetry_sdk::trace::SdkTracerProvider::builder()
        .with_batch_exporter(
            opentelemetry_otlp::SpanExporter::builder()
                .with_tonic()
                .build()?,
        )
        .with_resource(resource.clone())
        .build();

    let logger_provider = opentelemetry_sdk::logs::SdkLoggerProvider::builder()
        .with_batch_exporter(
            opentelemetry_otlp::LogExporter::builder()
                .with_tonic()
                .build()?,
        )
        .with_resource(resource)
        .build();

    Ok((tracer_provider, logger_provider))
}
