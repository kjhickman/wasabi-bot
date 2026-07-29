use topcoat::{
    Result,
    view::{Attributes, View, class, component, view},
};

/// The visual style of a [`button`].
///
/// [`Default`] is `ButtonVariant::Primary`, used when no variant is given.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum ButtonVariant {
    /// The primary-filled button for the main action.
    #[default]
    Primary,
    /// A muted, tinted fill for secondary actions.
    Secondary,
    /// A hairline-bordered button on the page background.
    Outline,
    /// No fill until hovered, for toolbars and inline actions.
    Ghost,
}

impl ButtonVariant {
    /// The Tailwind classes for this variant.
    ///
    /// Hover and press states apply the fill or foreground color at reduced
    /// opacity, so they hold up in both color schemes without `dark:`
    /// overrides. Every variant with a resting fill or border casts the
    /// theme's control shadow; `Ghost` is flat until hovered, so it casts
    /// none.
    ///
    /// Each variant sets its own border color rather than inheriting a
    /// transparent one from [`BASE`]: with two border-color classes on the
    /// same element, stylesheet order (not class order) would decide the
    /// winner.
    const fn classes(self) -> &'static str {
        match self {
            Self::Primary => {
                "border-transparent bg-primary text-primary-foreground shadow-xs \
                 hover:bg-primary/90 active:bg-primary/80"
            }
            Self::Secondary => {
                "border-transparent bg-foreground/5 text-foreground shadow-xs \
                 hover:bg-foreground/10 active:bg-foreground/15"
            }
            Self::Outline => {
                "border-border text-foreground shadow-xs hover:bg-foreground/5 \
                 active:bg-foreground/10"
            }
            Self::Ghost => {
                "border-transparent text-foreground hover:bg-foreground/5 active:bg-foreground/10"
            }
        }
    }
}

/// The size of a [`button`].
///
/// [`Default`] is `ButtonSize::Md`, used when no size is given.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum ButtonSize {
    /// The standard button size.
    #[default]
    Md,
    /// A square button sized for a single icon.
    Icon,
}

impl ButtonSize {
    /// The Tailwind classes for this size.
    ///
    /// Each size sets a text size, which also scales any icons inside: the
    /// `icon` component is `1em` square by default.
    const fn classes(self) -> &'static str {
        match self {
            Self::Md => "h-9 gap-2 rounded-lg px-4 text-sm",
            Self::Icon => "size-9 rounded-lg text-base",
        }
    }
}

/// The classes shared by every button, regardless of variant or size.
///
/// Every button carries a border (colored per variant) so that the `Outline`
/// variant, which only recolors it, does not change the button's dimensions.
const BASE: &str = "inline-flex shrink-0 items-center justify-center border \
    font-medium whitespace-nowrap transition-colors outline-none select-none \
    focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 \
    focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-50";

/// A button component.
///
/// The `variant` and `size` parameters select the styling, defaulting to
/// `Primary` and `Md`. The `attrs` (such as `class`, `type`, `disabled`, or
/// event handlers) are forwarded to the underlying `<button>`; a `class` among
/// them is appended to the computed classes. Child nodes become the button's
/// content.
///
/// ```ignore
/// view! { button(attrs: attributes! { type="submit" }, "Save") }
/// ```
#[component]
pub async fn button(
    #[default] variant: ButtonVariant,
    #[default] size: ButtonSize,
    #[default] mut attrs: Attributes,
    #[default] child: View,
) -> Result {
    view! {
        <button
            class=(class!(
                BASE,
                variant.classes(),
                size.classes(),
                attrs.remove("class"),
            ))
            (attrs)
        >
            (child)
        </button>
    }
}
