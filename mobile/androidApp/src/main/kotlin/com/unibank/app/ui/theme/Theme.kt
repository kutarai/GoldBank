package com.unibank.app.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import com.unibank.shared.domain.model.BrandingConfig

@Composable
fun UniBankTheme(
    branding: BrandingConfig = BrandingConfig.DEFAULT,
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    val primary = parseColor(branding.primaryColor)
    val secondary = parseColor(branding.secondaryColor)
    val tertiary = parseColor(branding.accentColor)

    val colorScheme = if (darkTheme) {
        darkColorScheme(
            primary = primary,
            onPrimary = Color.White,
            secondary = secondary,
            onSecondary = Color.Black,
            tertiary = tertiary,
            background = Color(0xFF121212),
            surface = Color(0xFF1E1E1E),
            error = ErrorRed,
            onError = OnErrorWhite,
        )
    } else {
        lightColorScheme(
            primary = primary,
            onPrimary = Color.White,
            secondary = secondary,
            onSecondary = Color.Black,
            tertiary = tertiary,
            background = Color(0xFFFAFAFA),
            surface = Color.White,
            error = ErrorRed,
            onError = OnErrorWhite,
        )
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = UniBankTypography,
        shapes = UniBankShapes,
        content = content,
    )
}
