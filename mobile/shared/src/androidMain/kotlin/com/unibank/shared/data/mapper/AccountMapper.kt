package com.unibank.shared.data.mapper

import com.unibank.shared.domain.model.*
import unibank.v1.accounts.AccountServiceOuterClass as Proto

object AccountMapper {

    fun toRegistrationResult(response: Proto.RegisterResponse) = RegistrationResult(
        success = response.success,
        message = response.message,
        registrationId = response.registrationId,
        otpLength = response.otpLength,
        otpTtlSeconds = response.otpTtlSeconds,
    )

    fun toOtpVerificationResult(response: Proto.VerifyOTPResponse) = OtpVerificationResult(
        success = response.success,
        message = response.message,
        accountId = response.accountId,
        temporaryToken = response.temporaryToken,
    )

    fun toAuthTokensFromPin(response: Proto.CreatePINResponse, accountId: String) = AuthTokens(
        accessToken = response.authToken,
        refreshToken = response.refreshToken,
        accessTokenExpiresIn = 3600,
        refreshTokenExpiresIn = 86400,
        accountId = accountId,
    )

    fun toAuthResult(response: Proto.AuthenticateResponse): AuthResult {
        if (response.success) {
            return AuthResult.Success(
                AuthTokens(
                    accessToken = response.accessToken,
                    refreshToken = response.refreshToken,
                    accessTokenExpiresIn = response.accessTokenExpiresIn,
                    refreshTokenExpiresIn = response.refreshTokenExpiresIn,
                    accountId = response.accountId,
                )
            )
        }
        if (response.lockoutRemainingSeconds > 0) {
            return AuthResult.LockedOut(response.lockoutRemainingSeconds)
        }
        return AuthResult.Failed(response.message, response.remainingAttempts)
    }

    fun toAuthTokensFromRefresh(response: Proto.RefreshTokenResponse, accountId: String) = AuthTokens(
        accessToken = response.accessToken,
        refreshToken = response.refreshToken,
        accessTokenExpiresIn = response.accessTokenExpiresIn,
        refreshTokenExpiresIn = response.refreshTokenExpiresIn,
        accountId = accountId,
    )

    fun toBalance(response: Proto.BalanceResponse) = Balance(
        accountId = response.accountId,
        balance = toMoney(response.balance),
        availableBalance = toMoney(response.availableBalance),
        dailyLimit = toMoney(response.dailyLimit),
        dailyUsed = toMoney(response.dailyUsed),
    )

    fun toProfile(response: Proto.ProfileResponse) = Profile(
        accountId = response.accountId,
        phoneNumber = response.phoneNumber,
        firstName = response.firstName,
        lastName = response.lastName,
        email = response.email,
        dateOfBirth = response.dateOfBirth,
        nationalId = response.nationalId,
        status = toAccountStatus(response.status),
        kycLevel = response.kycLevel,
        createdAt = response.createdAt?.let { "${it.seconds}" } ?: "",
        lastLoginAt = response.lastLoginAt?.let { "${it.seconds}" } ?: "",
    )

    fun toDeviceTransferInit(response: Proto.InitiateDeviceTransferResponse) = DeviceTransferInitResult(
        transferReference = response.transferReference,
        message = response.message,
        otpExpirySeconds = response.otpExpirySeconds,
    )

    fun toDeviceTransferComplete(response: Proto.CompleteDeviceTransferResponse) = DeviceTransferCompleteResult(
        success = response.success,
        message = response.message,
    )

    fun toTransaction(response: Proto.TransactionResponse) = Transaction(
        transactionId = response.transactionId,
        type = toTransactionType(response.type),
        amount = toMoney(response.amount),
        fee = toMoney(response.fee),
        status = toTransactionStatus(response.status),
        reference = response.reference,
        description = response.description,
        counterpartyName = response.counterpartyName,
        counterpartyPhone = response.counterpartyPhone,
        balanceAfter = toMoney(response.balanceAfter),
        createdAt = response.createdAt?.let { "${it.seconds}" } ?: "",
        completedAt = response.completedAt?.let { "${it.seconds}" } ?: "",
    )

    private fun toMoney(proto: unibank.v1.common.Common.Money?): Money {
        if (proto == null) return Money.ZERO_ZWG
        return Money(amount = proto.amount, currency = proto.currency.ifEmpty { "ZWG" })
    }

    private fun toAccountStatus(proto: Proto.AccountStatus) = when (proto) {
        Proto.AccountStatus.ACCOUNT_STATUS_PENDING_KYC -> AccountStatus.PENDING_KYC
        Proto.AccountStatus.ACCOUNT_STATUS_ACTIVE -> AccountStatus.ACTIVE
        Proto.AccountStatus.ACCOUNT_STATUS_SUSPENDED -> AccountStatus.SUSPENDED
        Proto.AccountStatus.ACCOUNT_STATUS_CLOSED -> AccountStatus.CLOSED
        Proto.AccountStatus.ACCOUNT_STATUS_FROZEN -> AccountStatus.FROZEN
        else -> AccountStatus.UNSPECIFIED
    }

    private fun toTransactionType(proto: Proto.TransactionType) = when (proto) {
        Proto.TransactionType.TRANSACTION_TYPE_CASH_IN -> TransactionType.CASH_IN
        Proto.TransactionType.TRANSACTION_TYPE_CASH_OUT -> TransactionType.CASH_OUT
        Proto.TransactionType.TRANSACTION_TYPE_P2P_SEND -> TransactionType.P2P_SEND
        Proto.TransactionType.TRANSACTION_TYPE_P2P_RECEIVE -> TransactionType.P2P_RECEIVE
        Proto.TransactionType.TRANSACTION_TYPE_PAYMENT_NFC -> TransactionType.PAYMENT_NFC
        Proto.TransactionType.TRANSACTION_TYPE_PAYMENT_QR -> TransactionType.PAYMENT_QR
        Proto.TransactionType.TRANSACTION_TYPE_BILL_PAYMENT -> TransactionType.BILL_PAYMENT
        Proto.TransactionType.TRANSACTION_TYPE_TRANSFER_DOMESTIC -> TransactionType.TRANSFER_DOMESTIC
        Proto.TransactionType.TRANSACTION_TYPE_TRANSFER_CROSS_BORDER -> TransactionType.TRANSFER_CROSS_BORDER
        Proto.TransactionType.TRANSACTION_TYPE_FEE -> TransactionType.FEE
        Proto.TransactionType.TRANSACTION_TYPE_REVERSAL -> TransactionType.REVERSAL
        Proto.TransactionType.TRANSACTION_TYPE_SETTLEMENT -> TransactionType.SETTLEMENT
        else -> TransactionType.UNSPECIFIED
    }

    private fun toTransactionStatus(proto: Proto.TransactionStatus) = when (proto) {
        Proto.TransactionStatus.TRANSACTION_STATUS_PENDING -> TransactionStatus.PENDING
        Proto.TransactionStatus.TRANSACTION_STATUS_PROCESSING -> TransactionStatus.PROCESSING
        Proto.TransactionStatus.TRANSACTION_STATUS_COMPLETED -> TransactionStatus.COMPLETED
        Proto.TransactionStatus.TRANSACTION_STATUS_FAILED -> TransactionStatus.FAILED
        Proto.TransactionStatus.TRANSACTION_STATUS_REVERSED -> TransactionStatus.REVERSED
        else -> TransactionStatus.UNSPECIFIED
    }
}
