fn main() {
    topcoat::icon::iconify::BuildConfig::new()
        .icon_set_version("lucide", "1.2.120")
        .stage()
        .unwrap();

    topcoat::tailwind::BuildConfig::new()
        .input("src/web/styles.css")
        .render()
        .unwrap();

    use std::hash::{Hash, Hasher};
    let css = std::fs::read(
        std::path::Path::new(&std::env::var("OUT_DIR").unwrap()).join("tailwind.css"),
    )
    .unwrap();
    let mut hasher = std::collections::hash_map::DefaultHasher::new();
    css.hash(&mut hasher);
    println!("cargo:rustc-env=WASABI_CSS_VERSION={:x}", hasher.finish());
}
