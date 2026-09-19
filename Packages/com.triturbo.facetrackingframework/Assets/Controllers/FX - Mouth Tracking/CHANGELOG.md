# CHANGELOG

## FX - Mouth Tracking

### 1.0.5
- Added MouthPress blend.


### 1.0.4
- Moved the **Mouth Tracking BlendTree** into an external asset (`Animations/Mouth Tracking/Mouth Tracking BlendTree.controller`) so both **WD On** and **WD Off** reference the same BlendTree. This prevents duplicate edits.
- Use `FT/MouthTrackingBlend` to blend **Mouth Tracking BlendTree** instead of `MouthTrackingEnable`


### 1.0.3
- Added Cheek Squint Blend.

### 1.0.2
- Face tracking is now automatically disabled when MMD Station is active. (WD On only)
- Adjusted the threshold for parameter:`Drive/FT/v2/TongueOut` to improve tongue movement behavior for `TongueNeutralMouthMorph*`.

### 1.0.1
- Added support for `MouthLeftOpenMorph*`
- Added support for `MouthRightOpenMorph*`

### 1.0.0
- Initial release.