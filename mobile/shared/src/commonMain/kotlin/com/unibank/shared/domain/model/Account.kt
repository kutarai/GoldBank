package com.unibank.shared.domain.model

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
