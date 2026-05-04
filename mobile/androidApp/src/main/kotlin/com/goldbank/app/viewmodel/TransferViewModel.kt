package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.TransferGrpcClient
import com.goldbank.shared.domain.model.TransferResult
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class TransferViewModel(
    private val transferClient: TransferGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow<TransferUiState>(TransferUiState.Idle)
    val uiState: StateFlow<TransferUiState> = _uiState.asStateFlow()

    fun sendP2P(
        recipientPhone: String,
        amount: String,
        currency: String = "ZWG",
        description: String = "",
        pin: String,
    ) {
        _uiState.value = TransferUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = transferClient.sendP2P(
                senderAccountId = accountId,
                recipientPhone = recipientPhone,
                amount = amount,
                currency = currency,
                description = description,
                pin = pin,
            )) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = TransferUiState.Success(result.data)
                    } else {
                        _uiState.value = TransferUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = TransferUiState.Error(result.error.message)
            }
        }
    }

    fun sendCrossBorder(
        recipientPhone: String,
        recipientName: String,
        recipientCountry: String,
        sendAmount: String,
        sendCurrency: String = "ZWG",
        receiveCurrency: String,
        corridorId: String,
        pin: String,
    ) {
        _uiState.value = TransferUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = transferClient.sendCrossBorder(
                senderAccountId = accountId,
                recipientPhone = recipientPhone,
                recipientName = recipientName,
                recipientCountry = recipientCountry,
                sendAmount = sendAmount,
                sendCurrency = sendCurrency,
                receiveCurrency = receiveCurrency,
                corridorId = corridorId,
                pin = pin,
            )) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = TransferUiState.Success(result.data)
                    } else {
                        _uiState.value = TransferUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = TransferUiState.Error(result.error.message)
            }
        }
    }

    fun resetState() { _uiState.value = TransferUiState.Idle }
}

sealed interface TransferUiState {
    data object Idle : TransferUiState
    data object Loading : TransferUiState
    data class Success(val result: TransferResult) : TransferUiState
    data class Error(val message: String) : TransferUiState
}
