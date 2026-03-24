package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.AiGrpcClient
import com.unibank.shared.data.remote.grpc.AssetGrpcClient
import com.unibank.shared.domain.model.AssetDetail
import com.unibank.shared.domain.model.AssetSummary
import com.unibank.shared.domain.model.DepositReceiptOcr
import com.unibank.shared.domain.model.PortfolioValue
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class AssetUiState(
    val assets: List<AssetSummary> = emptyList(),
    val selectedAsset: AssetDetail? = null,
    val portfolioValue: PortfolioValue? = null,
    val receiptOcr: DepositReceiptOcr? = null,
    val isLoading: Boolean = false,
    val isRegistering: Boolean = false,
    val error: String? = null,
)

class AssetViewModel(
    private val assetClient: AssetGrpcClient,
    private val aiClient: AiGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(AssetUiState())
    val uiState: StateFlow<AssetUiState> = _uiState.asStateFlow()

    private val accountId: String
        get() = sessionManager.getAccountId() ?: ""

    fun loadAssets() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            when (val result = assetClient.listMyAssets(accountId)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        assets = result.data,
                        isLoading = false,
                    )
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        error = result.error.message ?: "Failed to load assets",
                    )
                }
            }
        }
    }

    fun loadAssetDetail(assetId: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            when (val result = assetClient.getAssetDetail(accountId, assetId)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        selectedAsset = result.data,
                        isLoading = false,
                    )
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        error = result.error.message ?: "Failed to load asset detail",
                    )
                }
            }
        }
    }

    fun loadPortfolio() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            when (val result = assetClient.getPortfolioValue(accountId)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        portfolioValue = result.data,
                        isLoading = false,
                    )
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        error = result.error.message ?: "Failed to load portfolio",
                    )
                }
            }
        }
    }

    fun extractReceipt(imageBytes: ByteArray) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            when (val result = aiClient.extractDepositReceipt(imageBytes)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        receiptOcr = result.data,
                        isLoading = false,
                    )
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        error = result.error.message ?: "Failed to extract receipt data",
                    )
                }
            }
        }
    }

    fun registerAsset(
        receiptNumber: String,
        assetType: String,
        description: String,
        quantity: String,
        unit: String,
        weightGrams: String,
        purity: String,
        receiptImagePath: String,
        depositHouseId: String,
    ) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isRegistering = true, error = null)
            when (val result = assetClient.registerAsset(
                accountId = accountId,
                receiptNumber = receiptNumber,
                assetType = assetType,
                description = description,
                quantity = quantity,
                unit = unit,
                weightGrams = weightGrams,
                purity = purity,
                receiptImagePath = receiptImagePath,
                depositHouseId = depositHouseId,
            )) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        assets = _uiState.value.assets + result.data,
                        isRegistering = false,
                    )
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(
                        isRegistering = false,
                        error = result.error.message ?: "Failed to register asset",
                    )
                }
            }
        }
    }

    fun requestRelease(assetId: String, reason: String) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            when (val result = assetClient.requestRelease(accountId, assetId, reason)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(isLoading = false)
                    // Refresh the asset list to reflect updated status
                    loadAssets()
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(
                        isLoading = false,
                        error = result.error.message ?: "Failed to request release",
                    )
                }
            }
        }
    }

    fun clearOcr() {
        _uiState.value = _uiState.value.copy(receiptOcr = null)
    }
}
