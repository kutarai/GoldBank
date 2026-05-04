package com.goldbank.shared.domain.util

object MoneyFormatter {

    private val currencySymbols = mapOf(
        "ZWG" to "ZiG",
        "USD" to "$",
        "ZAR" to "R",
        "BWP" to "P",
        "GBP" to "£",
        "EUR" to "€",
    )

    fun format(amount: String, currency: String = "ZWG"): String {
        val symbol = currencySymbols[currency] ?: currency
        val decimal = amount.toBigDecimalOrNull() ?: return "$symbol 0.00"
        val formatted = decimal.setScale(2, java.math.RoundingMode.HALF_UP).toPlainString()
        return "$symbol $formatted"
    }

    fun formatCompact(amount: String, currency: String = "ZWG"): String {
        val symbol = currencySymbols[currency] ?: currency
        val decimal = amount.toBigDecimalOrNull() ?: return "${symbol}0"
        return when {
            decimal >= 1_000_000.toBigDecimal() -> "$symbol${(decimal / 1_000_000.toBigDecimal()).setScale(1, java.math.RoundingMode.HALF_UP)}M"
            decimal >= 1_000.toBigDecimal() -> "$symbol${(decimal / 1_000.toBigDecimal()).setScale(1, java.math.RoundingMode.HALF_UP)}K"
            else -> format(amount, currency)
        }
    }
}
