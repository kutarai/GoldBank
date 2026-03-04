package com.unibank.shared.data.remote.grpc

import com.unibank.shared.data.mapper.AccountMapper
import com.unibank.shared.data.remote.grpcCall
import com.unibank.shared.domain.model.*
import com.unibank.shared.domain.util.Result
import io.grpc.ManagedChannel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import unibank.v1.accounts.AccountServiceGrpcKt.AccountServiceCoroutineStub
import unibank.v1.accounts.AccountServiceOuterClass.*

class AccountGrpcClient(channel: ManagedChannel) {

    private val stub = AccountServiceCoroutineStub(channel)

    suspend fun register(phoneNumber: String, deviceId: String, tenantId: String): Result<RegistrationResult> =
        grpcCall {
            val request = RegisterRequest.newBuilder()
                .setPhoneNumber(phoneNumber)
                .setDeviceId(deviceId)
                .setTenantId(tenantId)
                .build()
            AccountMapper.toRegistrationResult(stub.register(request))
        }

    suspend fun verifyOtp(registrationId: String, otp: String, phoneNumber: String): Result<OtpVerificationResult> =
        grpcCall {
            val request = VerifyOTPRequest.newBuilder()
                .setRegistrationId(registrationId)
                .setOtp(otp)
                .setPhoneNumber(phoneNumber)
                .build()
            AccountMapper.toOtpVerificationResult(stub.verifyOTP(request))
        }

    suspend fun createPin(accountId: String, pin: String, pinConfirmation: String): Result<AuthTokens> =
        grpcCall {
            val request = CreatePINRequest.newBuilder()
                .setAccountId(accountId)
                .setPin(pin)
                .setPinConfirmation(pinConfirmation)
                .build()
            val response = stub.createPIN(request)
            if (!response.success) {
                throw io.grpc.StatusRuntimeException(
                    io.grpc.Status.INTERNAL.withDescription(response.message.ifEmpty { "PIN creation failed" })
                )
            }
            AccountMapper.toAuthTokensFromPin(response, accountId)
        }

    suspend fun authenticate(
        phoneNumber: String,
        pin: String,
        deviceId: String,
        tenantId: String,
    ): Result<AuthResult> = grpcCall {
        val request = AuthenticateRequest.newBuilder()
            .setPhoneNumber(phoneNumber)
            .setPin(pin)
            .setDeviceId(deviceId)
            .setTenantId(tenantId)
            .build()
        AccountMapper.toAuthResult(stub.authenticate(request))
    }

    suspend fun refreshToken(refreshToken: String, deviceId: String, accountId: String): Result<AuthTokens> =
        grpcCall {
            val request = RefreshTokenRequest.newBuilder()
                .setRefreshToken(refreshToken)
                .setDeviceId(deviceId)
                .build()
            AccountMapper.toAuthTokensFromRefresh(stub.refreshToken(request), accountId)
        }

    suspend fun logout(accountId: String, allDevices: Boolean): Result<Unit> = grpcCall {
        val request = LogoutRequest.newBuilder()
            .setAccountId(accountId)
            .setAllDevices(allDevices)
            .build()
        stub.logout(request)
        Unit
    }

    suspend fun updateProfile(
        accountId: String,
        firstName: String?,
        lastName: String?,
        email: String?,
        dateOfBirth: String?,
        nationalId: String?,
    ): Result<Profile> = grpcCall {
        val builder = UpdateProfileRequest.newBuilder().setAccountId(accountId)
        firstName?.let { builder.firstName = com.google.protobuf.StringValue.of(it) }
        lastName?.let { builder.lastName = com.google.protobuf.StringValue.of(it) }
        email?.let { builder.email = com.google.protobuf.StringValue.of(it) }
        dateOfBirth?.let { builder.dateOfBirth = com.google.protobuf.StringValue.of(it) }
        nationalId?.let { builder.nationalId = com.google.protobuf.StringValue.of(it) }
        AccountMapper.toProfile(stub.updateProfile(builder.build()))
    }

    suspend fun initiateDeviceTransfer(
        phoneNumber: String,
        newDeviceId: String,
    ): Result<DeviceTransferInitResult> = grpcCall {
        val request = InitiateDeviceTransferRequest.newBuilder()
            .setPhoneNumber(phoneNumber)
            .setNewDeviceId(newDeviceId)
            .build()
        AccountMapper.toDeviceTransferInit(stub.initiateDeviceTransfer(request))
    }

    suspend fun completeDeviceTransfer(
        transferReference: String,
        otp: String,
        pin: String,
        newDeviceId: String,
    ): Result<DeviceTransferCompleteResult> = grpcCall {
        val request = CompleteDeviceTransferRequest.newBuilder()
            .setTransferReference(transferReference)
            .setOtp(otp)
            .setPin(pin)
            .setNewDeviceId(newDeviceId)
            .build()
        AccountMapper.toDeviceTransferComplete(stub.completeDeviceTransfer(request))
    }

    suspend fun getProfile(accountId: String): Result<Profile> = grpcCall {
        val request = GetProfileRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        AccountMapper.toProfile(stub.getProfile(request))
    }

    suspend fun getBalance(accountId: String): Result<Balance> = grpcCall {
        val request = GetBalanceRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        AccountMapper.toBalance(stub.getBalance(request))
    }

    fun getTransactions(accountId: String): Flow<Transaction> {
        val request = GetTransactionsRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        return stub.getTransactions(request).map { AccountMapper.toTransaction(it) }
    }
}
