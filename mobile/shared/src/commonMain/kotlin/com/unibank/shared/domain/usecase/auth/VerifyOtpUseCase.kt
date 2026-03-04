package com.unibank.shared.domain.usecase.auth

import com.unibank.shared.domain.model.OtpVerificationResult
import com.unibank.shared.domain.repository.AuthRepository
import com.unibank.shared.domain.util.Result

class VerifyOtpUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        registrationId: String,
        otp: String,
        phoneNumber: String,
    ): Result<OtpVerificationResult> =
        authRepository.verifyOtp(registrationId, otp, phoneNumber)
}
