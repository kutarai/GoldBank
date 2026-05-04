package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.AiGrpcClient
import com.goldbank.shared.data.remote.grpc.AssetGrpcClient
import com.goldbank.shared.domain.model.AccountSummary
import com.goldbank.shared.domain.model.Balance
import com.goldbank.shared.domain.model.PortfolioValue
import com.goldbank.shared.domain.model.Profile
import com.goldbank.shared.domain.model.SpendingInsight
import com.goldbank.shared.domain.model.Transaction
import com.goldbank.shared.domain.usecase.account.GetBalanceUseCase
import com.goldbank.shared.domain.usecase.account.GetProfileUseCase
import com.goldbank.shared.domain.usecase.account.GetTransactionsUseCase
import com.goldbank.shared.domain.usecase.auth.LogoutUseCase
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.launch

class HomeViewModel(
    private val getBalanceUseCase: GetBalanceUseCase,
    private val getProfileUseCase: GetProfileUseCase,
    private val getTransactionsUseCase: GetTransactionsUseCase,
    private val logoutUseCase: LogoutUseCase,
    private val sessionManager: SessionManager,
    private val aiClient: AiGrpcClient,
    private val assetClient: AssetGrpcClient,
) : ViewModel() {

    private val _uiState = MutableStateFlow(HomeUiState())
    val uiState: StateFlow<HomeUiState> = _uiState.asStateFlow()

    private val primaryAccountId: String
        get() = sessionManager.getAccountId() ?: ""

    private val accountId: String
        get() = _uiState.value.selectedAccountId ?: primaryAccountId

    init {
        loadDashboard()
    }

    fun loadDashboard() {
        loadProfile()
        loadBalance()
        loadRecentTransactions()
        loadSpendingInsights()
        loadPortfolio()
    }

    fun loadPortfolio() {
        val accountId = sessionManager.getAccountId() ?: return
        viewModelScope.launch {
            when (val result = assetClient.getPortfolioValue(accountId)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(portfolioValue = result.data)
                }
                is Result.Failure -> { /* Non-critical — silently skip if assets unavailable */ }
            }
        }
    }

    fun loadSpendingInsights() {
        val accountId = sessionManager.getAccountId() ?: return
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isInsightsLoading = true)
            when (val result = aiClient.getSpendingInsights(accountId)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        spendingInsights = result.data.insights,
                        isInsightsLoading = false,
                    )
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(isInsightsLoading = false)
                }
            }
        }
    }

    fun selectAccount(account: AccountSummary) {
        _uiState.value = _uiState.value.copy(selectedAccountId = account.accountId)
        loadBalance()
        loadRecentTransactions()
    }

    private fun loadBalance() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isBalanceLoading = true)
            when (val result = getBalanceUseCase(accountId)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        balance = result.data,
                        isBalanceLoading = false,
                        balanceError = null,
                    )
                }
                is Result.Failure -> {
                    _uiState.value = _uiState.value.copy(
                        isBalanceLoading = false,
                        balanceError = "Failed to load balance",
                    )
                }
            }
        }
    }

    private fun loadProfile() {
        viewModelScope.launch {
            when (val result = getProfileUseCase(accountId)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(profile = result.data)
                }
                is Result.Failure -> { /* Silently fail — profile is non-critical for home */ }
            }
        }
    }

    private fun loadRecentTransactions() {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isTransactionsLoading = true)
            val transactions = mutableListOf<Transaction>()
            getTransactionsUseCase(accountId)
                .catch {
                    _uiState.value = _uiState.value.copy(
                        isTransactionsLoading = false,
                        transactionsError = "Failed to load transactions",
                    )
                }
                .collect { transaction ->
                    transactions.add(transaction)
                    _uiState.value = _uiState.value.copy(
                        recentTransactions = transactions.toList(),
                        isTransactionsLoading = false,
                    )
                }
            // Stream ended
            _uiState.value = _uiState.value.copy(isTransactionsLoading = false)
        }
    }

    fun logout() {
        viewModelScope.launch {
            logoutUseCase(accountId)
        }
    }
}

data class HomeUiState(
    val balance: Balance? = null,
    val profile: Profile? = null,
    val recentTransactions: List<Transaction> = emptyList(),
    val isBalanceLoading: Boolean = false,
    val isTransactionsLoading: Boolean = false,
    val balanceError: String? = null,
    val transactionsError: String? = null,
    val selectedAccountId: String? = null,
    val spendingInsights: List<SpendingInsight> = emptyList(),
    val isInsightsLoading: Boolean = false,
    val portfolioValue: PortfolioValue? = null,
) {
    val accounts: List<AccountSummary> get() = profile?.accounts ?: emptyList()
    val selectedAccount: AccountSummary? get() =
        accounts.firstOrNull { it.accountId == selectedAccountId } ?: accounts.firstOrNull()
}
