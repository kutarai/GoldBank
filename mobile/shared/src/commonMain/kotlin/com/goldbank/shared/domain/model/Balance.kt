package com.goldbank.shared.domain.model

data class Balance(
    val accountId: String,
    val balance: Money,
    val availableBalance: Money,
    val dailyLimit: Money,
    val dailyUsed: Money,
)
