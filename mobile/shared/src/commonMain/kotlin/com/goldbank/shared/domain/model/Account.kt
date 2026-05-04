package com.goldbank.shared.domain.model

data class Profile(
    val accountId: String,
    val phoneNumber: String,
    val firstName: String,
    val lastName: String,
    val email: String,
    val dateOfBirth: String,
    val nationalId: String,
    val status: AccountStatus,
    val kycLevel: Int,
    val createdAt: String,
    val lastLoginAt: String,
    val accounts: List<AccountSummary> = emptyList(),
)

data class AccountSummary(
    val accountId: String,
    val currency: String,
    val balance: Money,
    val availableBalance: Money,
    val cardPanLast4: String,
)

data class DeviceTransferInitResult(
    val transferReference: String,
    val message: String,
    val otpExpirySeconds: Int,
)

data class DeviceTransferCompleteResult(
    val success: Boolean,
    val message: String,
)

enum class AccountStatus {
    UNSPECIFIED,
    PENDING_KYC,
    ACTIVE,
    SUSPENDED,
    CLOSED,
    FROZEN,
}
