package com.goldbank.app.ui.components

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp

@Composable
fun OtpInput(
    value: String,
    onValueChange: (String) -> Unit,
    otpLength: Int = 6,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    isError: Boolean = false,
    onComplete: (String) -> Unit = {},
) {
    val focusRequester = remember { FocusRequester() }

    LaunchedEffect(Unit) {
        focusRequester.requestFocus()
    }

    BasicTextField(
        value = value,
        onValueChange = { newValue ->
            val filtered = newValue.filter { it.isDigit() }.take(otpLength)
            onValueChange(filtered)
            if (filtered.length == otpLength) {
                onComplete(filtered)
            }
        },
        modifier = modifier
            .fillMaxWidth()
            .focusRequester(focusRequester),
        enabled = enabled,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
        decorationBox = {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp, Alignment.CenterHorizontally),
            ) {
                repeat(otpLength) { index ->
                    val char = value.getOrNull(index)?.toString() ?: ""
                    val isFocused = value.length == index
                    val borderColor = when {
                        isError -> MaterialTheme.colorScheme.error
                        isFocused -> MaterialTheme.colorScheme.primary
                        char.isNotEmpty() -> MaterialTheme.colorScheme.outline
                        else -> MaterialTheme.colorScheme.outlineVariant
                    }
                    Box(
                        modifier = Modifier
                            .size(48.dp)
                            .border(
                                width = if (isFocused) 2.dp else 1.dp,
                                color = borderColor,
                                shape = RoundedCornerShape(8.dp),
                            ),
                        contentAlignment = Alignment.Center,
                    ) {
                        Text(
                            text = char,
                            style = MaterialTheme.typography.headlineSmall,
                            textAlign = TextAlign.Center,
                        )
                    }
                }
            }
        },
    )
}
