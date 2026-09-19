# Changelog

## FX – Eye Tracking

### 1.0.4

- Moved the **Eye Tracking BlendTree** into an external asset (`Animations/Eye Tracking/Eye Tracking BlendTree.controller`) so both **WD On** and **WD Off** reference the same BlendTree. This prevents duplicate edits.
- Added blending for BrowDown@EyeOpen when the eyelid openness is ≥ 0.75.

### 1.0.3

- Fixed: Eye tracking can now be enabled while in **Station**.

### 1.0.2

- Replaced `BrowExpression` with separate parameters:
  * `BrowInnerUp`
  * `BrowOuterUp`
  * `BrowLowerer`

### 1.0.1

- Added automatic face tracking disable when **MMD Station** is active (**WD On** only).

### 1.0.0

- Initial release.