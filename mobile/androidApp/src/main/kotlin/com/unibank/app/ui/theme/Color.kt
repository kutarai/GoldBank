package com.unibank.app.ui.theme

import androidx.compose.ui.graphics.Color

// Default UniBank colors (overridden by branding at runtime)
val Green900 = Color(0xFF1B5E20)
val Green800 = Color(0xFF2E7D32)
val Green700 = Color(0xFF388E3C)
val Green50 = Color(0xFFE8F5E9)

val YellowAccent = Color(0xFFFFD600)
val GreenAccent = Color(0xFF00C853)

val ErrorRed = Color(0xFFB00020)
val OnErrorWhite = Color(0xFFFFFFFF)

fun parseColor(hex: String): Color = try {
    Color(android.graphics.Color.parseColor(hex))
} catch (_: Exception) {
    Green900
}
