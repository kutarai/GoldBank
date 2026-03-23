package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.AiGrpcClient
import com.unibank.shared.domain.model.ChatMessage
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.launch

private const val RATE_LIMIT_MAX_MESSAGES = 20
private const val RATE_LIMIT_WINDOW_MS = 60L * 60L * 1000L // 1 hour

class ChatViewModel(
    private val aiClient: AiGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ChatUiState())
    val uiState: StateFlow<ChatUiState> = _uiState.asStateFlow()

    private var streamingJob: Job? = null

    // Timestamps of sent messages for rate limiting (rolling window)
    private val messageSentTimestamps = ArrayDeque<Long>()

    fun sendMessage(text: String) {
        if (text.isBlank()) return

        val now = System.currentTimeMillis()

        // Purge timestamps outside the rate-limit window
        while (messageSentTimestamps.isNotEmpty() &&
            now - messageSentTimestamps.first() > RATE_LIMIT_WINDOW_MS
        ) {
            messageSentTimestamps.removeFirst()
        }

        if (messageSentTimestamps.size >= RATE_LIMIT_MAX_MESSAGES) {
            _uiState.value = _uiState.value.copy(rateLimited = true)
            return
        }

        messageSentTimestamps.addLast(now)

        val userMessage = ChatMessage(
            role = "user",
            content = text,
            timestamp = now,
        )

        val updatedMessages = _uiState.value.messages + userMessage
        val updatedCount = _uiState.value.messageCount + 1

        _uiState.value = _uiState.value.copy(
            messages = updatedMessages,
            isStreaming = true,
            currentStreamText = "",
            error = null,
            rateLimited = false,
            messageCount = updatedCount,
        )

        val accountId = sessionManager.getAccountId() ?: ""

        streamingJob = viewModelScope.launch {
            aiClient.chat(
                accountId = accountId,
                message = text,
                history = updatedMessages.dropLast(1), // exclude the just-added user message
            )
                .catch { throwable ->
                    _uiState.value = _uiState.value.copy(
                        isStreaming = false,
                        currentStreamText = "",
                        error = throwable.message ?: "An error occurred",
                    )
                }
                .collect { response ->
                    if (response.isComplete) {
                        val assembledText = _uiState.value.currentStreamText
                        val assistantMessage = ChatMessage(
                            role = "assistant",
                            content = assembledText,
                            timestamp = System.currentTimeMillis(),
                        )
                        _uiState.value = _uiState.value.copy(
                            messages = _uiState.value.messages + assistantMessage,
                            isStreaming = false,
                            currentStreamText = "",
                            sessionId = response.sessionId.takeIf { it.isNotEmpty() }
                                ?: _uiState.value.sessionId,
                        )
                    } else {
                        _uiState.value = _uiState.value.copy(
                            currentStreamText = _uiState.value.currentStreamText + response.token,
                            sessionId = response.sessionId.takeIf { it.isNotEmpty() }
                                ?: _uiState.value.sessionId,
                        )
                    }
                }
        }
    }

    fun cancelStream() {
        streamingJob?.cancel()
        streamingJob = null
        _uiState.value = _uiState.value.copy(
            isStreaming = false,
            currentStreamText = "",
        )
    }

    fun clearChat() {
        cancelStream()
        messageSentTimestamps.clear()
        _uiState.value = ChatUiState()
    }
}

data class ChatUiState(
    val messages: List<ChatMessage> = emptyList(),
    val isStreaming: Boolean = false,
    val currentStreamText: String = "",
    val sessionId: String? = null,
    val error: String? = null,
    val messageCount: Int = 0,
    val rateLimited: Boolean = false,
)
