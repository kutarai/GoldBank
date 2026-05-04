package com.goldbank.app.ui.theme

import androidx.compose.ui.graphics.Color

// Default GoldBank colors (overridden by branding at runtime)
val Gold900 = Color(0xFF8B6F1A)
val Gold800 = Color(0xFFA88720)
val Gold700 = Color(0xFFC9A227)
val Gold50 = Color(0xFFFAF3DC)

val AmberAccent = Color(0xFFFFB300)
val BronzeAccent = Color(0xFF3D2E12)

val ErrorRed = Color(0xFFB00020)
val OnErrorWhite = Color(0xFFFFFFFF)

fun parseColor(hex: String): Color = try {
    Color(android.graphics.Color.parseColor(hex))
} catch (_: Exception) {
    Gold700
}
