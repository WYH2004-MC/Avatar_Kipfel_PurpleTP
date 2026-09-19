# Triturbo Face Tracking Framework Changelog

notable changes.
## [1.0.18] - 2025/12/06
### Fixed
- Fixed Eye Ear Motion BlendTree.
  Corrected an incorrect blendParameter that caused FT/SmoothedEyeY to behave improperly.

## [1.0.17] - 2025/11/13
### Fixed
- Mesh generation now correctly uses the updated `BlendShapeData`. Previously, the old data could be used even after an FBX update, which caused mismatches in the generated mesh.
### Changed
- The `"-----FT-----"` blendshape is now baked directly into the base mesh. This ensures the final mesh always includes the intended face tracking adjustments without relying on animation.

## [1.0.16] - 2025/11/03

### Fixed
#### **Localization:**
  - Fixed an issue where non-existent tooltip keys could still display tooltips.
  - Tooltips are now properly formatted only when valid and defined.

#### **FBX Version Detection:**
  - Fixed a problem where version detection could be incorrect if the original FBX asset was updated after mesh generation.
  - The system now uses metadata from the generated mesh for more accurate version checks.
#### Mesh Application:
  - Prevented exceptions when multiple SkinnedMeshRenderers share the same mesh name.


## [1.0.15] - 2025/11/02

### Changed
#### Eye & Ear Motion
- Ears now move up and down in sync with the eyes when looking up or down.

### Added
#### FX – Mouth Tracking [1.0.5]
- Added support for the **MouthPress** blendshape.

## [1.0.14] - 2025/10/27
### Added
- Support for displaying applied **BlendShape Data** assets directly in the installer.
- Support for mesh generation from **BlendShare GenerateMeshAssetSO**.
### Changed
- Now requires **BlendShare 1.0.0** or **1.0.0-User**.
### Fixed
- Set **Write Defaults** to **false** in Eye and Mouth controllers (WD Off).
### Chore
- Removed unnecessary Layer Control state machine behaviours from Eye and Mouth Tracking controllers.


## [1.0.13] - 2025/10/22
- Fix: Added missing animator parameters to the “FX - Eye Tracking (WD Off)” and “FX - Eye Tracking (WD On)” controllers.
- Improvement: Changed several private fields in FaceTrackingInstaller to protected for better extensibility. 
- Added `OnTargetAvatarChanged` event to notify when the target avatar is changed.
- Added a new string field `m_Tag` to the `FbxObjectInfo` class, allowing objects to be tagged for identification or categorization.

## [1.0.12] - 2025/10/10
- Fix: no longer set eyelid type to None when selecting Native Eyetracking in the installer.
- Fix: replaced `FT/DirectBlend` with `__QuantizationParameters/One` to prevent parameter default values from setting to 0 and causing tongue tracking to stop moving.

## [1.0.11] - 2025/10/09
- Reworked the tracking animator controller to fix auto-disable behavior
  when entering MMD in disabled state
- Prevents controller from turning off immediately when first enabled
- Adds support for AFK auto-disable

## [1.0.10] - 2025/10/07
- Fix: prevent `FT/MouthReset` from becoming ≤ 0 when MouthClosed exceeds JawOpen.
- Fix: update viseme compensate controller transitions after changing `MouthTrackingEnable` from float to bool.
- Feat: add applied state indicator to FaceTrackingInstaller UI.

## [1.0.9] - 2025/09/17
- Renamed blend parameter from `FT/MouthTrackingBlend` to `FT/DirectBlend` in Tongue Movement.controller.

## [1.0.8] - 2025/09/07
**Eye Tracking Controller v1.0.4 Update:**
- Moved the **Eye Tracking BlendTree** into an external asset (`Animations/Eye Tracking/Eye Tracking BlendTree.controller`) so both **WD On** and **WD Off** reference the same BlendTree. This prevents duplicate edits.
- Added blending for BrowDown@EyeOpen when the eyelid openness is 0.75.

**Mouth Tracking Controller v1.0.4 Update:**
- Refactored the Mouth Tracking BlendTree into an external asset (`Animations/Mouth Tracking/Mouth Tracking BlendTree.controller`) so both **WD On** and **WD Off** reference the same BlendTree. This avoids duplicate edits.
- Added `Mouth_Tracking_Enable` and `Mouth_Tracking_Disable` animation clips for toggling mouth tracking.
- Use new parameter `FT/MouthTrackingBlend` to blend **Mouth Tracking BlendTree** instead of `MouthTrackingEnable`

### Changed:
- Introduced new SmileFrownLeft and SmileFrownRight blend trees to the **Mouth Reset Parameter Gen** controller.
- Updated `FT/MouthReset` to decrease by `1` when `MouthClosed = 1` in **Mouth Reset Parameter Gen** controller.

### Fixed:
- Preview now reads blendshape weights from the reset clip instead of always resetting to 0.


## [1.0.7] - 2025/08/28
### Fixed:
- Reduced duplicate sub-parameter warning.
- Corrected the logic for matching layer parameters in EmoteLayersModifierUtil. Now, if m_LayerParameters is empty, the layer is added to the result immediately, ensuring correct behavior when no parameters are specified.

**Eye Tracking Controller v1.0.3 Update:**
- Fixed: Eye tracking can now be enabled while in **Station**.

## [1.0.6] - 2025/07/29
### Added:
- Added `EyeTracking_DetailedBrow` parameters config.
**Mouth Tracking Controller v1.0.3 Update:**
- Added to using `CheekSquint` parameters to control cheek squinting.

### Changed:
**Eye Tracking Controller v1.0.2 Update:**
- Replaced `BrowExpression` with separate parameters: `BrowInnerUp`, `BrowOuterUp`, and `BrowLowerer`.

**Parameter Config Updates:**
- Added `BrowInnerUp`, `BrowOuterUp`, and `BrowLowerer` as linked parameters, mapped to `FT/v2/BrowExpression`.
- Added `CheekSquint` as a linked parameter, mapped to `FT/v2/SmileFrown`.

### Fixed:
Updated parameter config: changed FT/v2/MouthRaiser to FT/v2/MouthRaiserLower.

## [1.0.5] - 2025/06/16
### Fixed
- Fixed a bug in ft installer where mouthSelection was incorrectly compared against the length of m_EyeTrackingPrefabs instead of m_MouthTrackingPrefabs.

## [1.0.4] - 2025/06/13
### Added
- Added Tongue Linked parameters config.
### Changed
- Removed 0.05 dead zone for JawOpen.

## [1.0.3] - 2025/05/11
### Added
- Button for opening Instalation Guide in the face tracking installer.
### Changed
- Updated `FX - Eye Tracking` to version 1.0.1.
- Updated `FX - Mouth Tracking` to version 1.0.2.

## [1.0.2] - 2025/03/10
### Changed
- Updated `FX - Mouth Tracking` to version 1.0.1.

## [1.0.1] - 2025/03/07
### Added
- VisemeCompensate prefab.

## [1.0.0] - 2025/02/28
- Initial release.