# Design System Strategy: The Methodical Muse

## 1. Overview & Creative North Star: "The Digital Curator"
This design system moves away from the chaotic, high-stimulation tropes of traditional idle games. Instead, it adopts the **Creative North Star of "The Digital Curator."** The experience is designed to feel like a high-end productivity dashboard—think Linear or Notion—reimagined for an incremental gaming environment. 

We achieve a signature look by prioritizing **Intentional Asymmetry** and **Tonal Depth**. Rather than a rigid, centered grid, we use wide gutters and "hanging" typography to create an editorial feel. The goal is to make the player feel like a sophisticated administrator of a complex system, where progress is measured through elegant data visualization rather than flashing lights.

---

## 2. Colors: Tonal Architecture
The palette is rooted in a warm, tactile cream base. We avoid the clinical coldness of pure white (#FFFFFF) to reduce eye strain during long "idling" sessions.

### The "No-Line" Rule
**Strict Mandate:** Designers are prohibited from using 1px solid borders to define primary sections. Boundaries must be established through **Tonal Transitions**. 
- Use `surface` for the global canvas.
- Use `surface_container_low` for secondary sidebars.
- Use `surface_container_highest` for active "hero" panels.
This creates a "molded" look where the UI feels like a single, continuous piece of ivory rather than a collection of boxes.

### Surface Hierarchy & Nesting
To create depth without shadows, use the "Nesting Logic":
- **Level 0 (Canvas):** `surface` (#fbf9f5)
- **Level 1 (Zones):** `surface_container` (#eeeee8)
- **Level 2 (Cards):** `surface_container_lowest` (#ffffff) — This creates a "lifted" effect because the card is brighter than its container.

### Signature Accents
- **Primary (`#426464`):** Used for "Productivity" or "Industrial" categories. Use a subtle gradient from `primary` to `primary_container` on high-value CTAs to add a "satin" finish.
- **Secondary (`#50616d`):** Used for "Science" or "Tech" categories.
- **Glassmorphism:** For floating modals or tooltips, use `surface` at 80% opacity with a `20px` backdrop-blur. This ensures the game world/dashboard remains visible but softly obscured behind the glass.

---

## 3. Typography: Editorial Authority
We utilize **Inter** for its mathematical precision and neutral character. The hierarchy is designed to make numbers—the soul of an idle game—feel like high-end financial data.

- **The Power Gap:** We use a high-contrast scale. `display-lg` (3.5rem) is used for the primary "Current Balance," while `label-sm` (0.6875rem) is used for technical metadata. This gap creates a sense of scale and importance.
- **Micro-Copy:** All `label-md` and `label-sm` elements should use `letter-spacing: 0.02em` and `font-weight: 500` to ensure legibility against the cream background.
- **Numerical Data:** For counters that increment rapidly, always use "tabular lining" (monospaced numbers) to prevent the UI from "jittering."

---

## 4. Elevation & Depth: Tonal Layering
Traditional elevation (Z-axis) in this system is achieved via **Light, not Shadow.**

- **The Layering Principle:** Place a `surface_container_lowest` (#ffffff) card inside a `surface_container_low` (#f5f4ef) parent. The shift in brightness mimics the way light hits the top sheet of a stack of paper.
- **Ambient Shadows:** Only use shadows for "Temporary" elements (Modals, Context Menus). Use the `on_surface` color at 4% opacity with a `32px` blur and `8px` Y-offset. It should feel like a soft glow, not a dark drop shadow.
- **The "Ghost Border" Fallback:** If a data table requires containment, use the `outline_variant` token at **15% opacity**. This provides a "suggestion" of a line that disappears into the background upon quick glance.

---

## 5. Components: The Industrial Kit

### Primary Action Buttons
- **Style:** Pill-shaped (`rounded-full`) or `lg` (1rem) corners.
- **Background:** Gradient from `primary` to `primary_dim`.
- **Text:** `on_primary` (#d9ffff), bold, all-caps for 16:9 dashboard prominence.
- **Interaction:** On hover, shift to `primary_fixed_dim`. No "pop-out" animations; use subtle color transitions (200ms ease).

### Progress Bars (The "Pulse" of Idle)
- **Track:** `surface_container_highest`.
- **Fill:** `secondary` for standard, `primary` for "boosted" states.
- **Design:** Forbid borders. The fill should have a 2px "inner-glow" using `primary_fixed` to make the progress feel energetic.

### Category Chips
- **Style:** Flat, no border.
- **Color:** `secondary_container` for the background and `on_secondary_container` for the text.
- **Usage:** Used to tag resource types (e.g., [ +2.5/s ] or [ Wood ]).

### Data Cards
- **Rule:** **No Divider Lines.** 
- **Separation:** Use `8px` vertical whitespace to separate the Title from the Body. 
- **Header:** Use `title-sm` in `on_surface_variant` for a muted, professional look.

### Input Fields
- **Style:** `surface_container_low` background. 
- **Focus State:** 2px solid `primary` border. The background should not change, only the border defines the focus.

---

## 6. Do's and Don'ts

### Do:
- **Embrace Whitespace:** If two elements feel crowded, add `16px` of space rather than a divider line.
- **Use Asymmetric Grids:** In a 16:9 layout, try a 30/70 split. Use the 30% side for navigation/stats and the 70% for the main "world" or "dashboard."
- **Align to Baseline:** Ensure all text labels in a row share the same baseline, regardless of font size.

### Don't:
- **No Pure Black:** Never use #000000. Use `on_background` (#30332e) for high-contrast text.
- **No Skeuomorphism:** No inner shadows, no "pressed" button states that look like physical plastic.
- **No 100% Opacity Borders:** If you must use a border, it must be semi-transparent.
- **No Harsh Corners:** Avoid `none` or `sm` roundedness unless it's for a technical "code-like" readout. Stick to `md` (0.75rem) for a friendly, modern app feel.