package com.goldbank.shared.domain.model

data class Money(
    val amount: String,
    val currency: String = "ZWG"
) {
    companion object {
        val ZERO_ZWG = Money("0.00", "ZWG")
    }
}
