# Refactor.Ugui.Bounder 0.1.0

Fits a `RectTransform` around its direct child `RectTransform`s after uGUI layout.

## Install

Add this Git URL in Unity Package Manager:

```text
https://github.com/refactor-op/com.refactor.ugui.bounder.git
```

## Use

Add `Bounder` to the parent rect. Choose whether each child contributes its full rect or only its pivot,
select the fitted axes, and configure padding. Inactive children contribute only when **Include Inactive** is enabled.

All active Bounders rebuild deepest-first during the canvas PostLayout pass, so a parent measures the fitted result of
its nested Bounders. Resizing preserves each direct child's world position and rect size.

## Sample

Import **Bounder Demo** from Package Manager and open `BounderDemo.unity`.
