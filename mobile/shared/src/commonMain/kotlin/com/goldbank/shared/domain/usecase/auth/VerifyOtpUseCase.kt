package com.goldbank.shared.domain.usecase.auth

import com.goldbank.shared.domain.model.OtpVerificationResult
import com.goldbank.shared.domain.repository.AuthRepository
import com.goldbank.shared.domain.util.Result

class VerifyOtpUseCase(private val authRepository: AuthRepository) {
    suspend operator fun invoke(
        registrationId: String,
        otp: String,
        phoneNumber: String,
    ): Result<OtpVerificationResult> =
        authRepository.verifyOtp(registrationId, otp, phoneNumber)
}
