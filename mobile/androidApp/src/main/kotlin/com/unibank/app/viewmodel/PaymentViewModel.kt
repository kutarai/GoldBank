package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.PaymentGrpcClient
import com.unibank.shared.domain.model.PaymentResult
import com.unibank.shared.domain.model.QrCode
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class PaymentViewModel(
    private val paymentClient: PaymentGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow<PaymentUiState>(PaymentUiState.Idle)
    val uiState: StateFlow<PaymentUiState> = _uiState.asStateFlow()

    fun generateQrCode(amount: String, currency: String = "ZWG", description: String = "") {
        _uiState.value = PaymentUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = paymentClient.generateQrCode(
                merchantId = accountId,
                terminalId = "",
                amount = amount,
                currency = currency,
                description = description,
                ttlSeconds = 300,
            )) {
                is Result.Success -> _uiState.value = PaymentUiState.QrGenerated(result.data)
                is Result.Failure -> _uiState.value = PaymentUiState.Error("Failed to generate QR code")
            }
        }
    }

    fun processQrPayment(qrCodeData: String, pin: String) {
        _uiState.value = PaymentUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = paymentClient.processQrPayment(accountId, qrCodeData, pin)) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = PaymentUiState.PaymentComplete(result.data)
                    } else {
                        _uiState.value = PaymentUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = PaymentUiState.Error("Payment failed")
            }
        }
    }

    fun tokenizeCard() {
        _uiState.value = PaymentUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = paymentClient.tokenizeCard(accountId, "", "android-${android.os.Build.SERIAL}")) {
                is Result.Success -> {
                    if (result.data.success) {
                        sessionManager.saveNfcToken(result.data.token)
                        _uiState.value = PaymentUiState.Tokenized
                    } else {
                        _uiState.value = PaymentUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = PaymentUiState.Error("Tokenization failed")
            }
        }
    }

    fun confirmNfcPayment(transactionId: String, pin: String) {
        _uiState.value = PaymentUiState.Loading
        viewModelScope.launch {
            when (val result = paymentClient.confirmNfcPayment(transactionId, pin)) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = PaymentUiState.PaymentComplete(result.data)
                    } else {
                        _uiState.value = PaymentUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = PaymentUiState.Error("Payment confirmation failed")
            }
        }
    }

    fun resetState() { _uiState.value = PaymentUiState.Idle }
}

sealed interface PaymentUiState {
    data object Idle : PaymentUiState
    data object Loading : PaymentUiState
    data class QrGenerated(val qrCode: QrCode) : PaymentUiState
    data class QrScanned(val qrData: String) : PaymentUiState
    data object Tokenized : PaymentUiState
    data class PaymentPending(val transactionId: String) : PaymentUiState
    data class PaymentComplete(val result: PaymentResult) : PaymentUiState
    data class Error(val message: String) : PaymentUiState
}
