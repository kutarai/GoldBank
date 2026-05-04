package com.goldbank.shared.data.remote

import android.util.Log
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.domain.repository.AuthRepository
import com.goldbank.shared.domain.util.Result
import kotlin.coroutines.AbstractCoroutineContextElement
import kotlin.coroutines.CoroutineContext
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

/**
 * Refreshes the access token before it expires.
 *
 * Called from [grpcCall] on every gRPC request. The fast-path is a single
 * timestamp comparison — actual refresh only happens when the token is within
 * 60 seconds of expiry (or already expired). The work is mutex-guarded so
 * concurrent in-flight calls don't all hit the network simultaneously.
 *
 * Re-entrancy: when the refresh request itself goes back through [grpcCall]
 * (AccountGrpcClient.refreshToken), we use a CoroutineContext element to skip
 * the check on the inner call so we don't deadlock on the mutex.
 *
 * If the refresh token is itself rejected, the session is dropped via
 * [SessionManager.logout] so the UI bounces to the login screen.
 */
class TokenRefresher(
    private val sessionManager: SessionManager,
    private val authRepository: AuthRepository,
    private val deviceIdProvider: () -> String,
) {
    private val mutex = Mutex()

    suspend fun ensureFresh() {
        // 1. Fast path — skip when we're already inside a refresh.
        if (currentCoroutineContext()[RefreshGuard] != null) return

        // 2. Fast path — skip when there's nothing to refresh against.
        val currentRefresh = sessionManager.getRefreshToken()
        if (currentRefresh.isNullOrBlank()) return

        // 3. Fast path — token still has plenty of life left.
        if (!sessionManager.isTokenExpiringSoon()) return

        mutex.withLock {
            // 4. Re-check inside the lock — another caller may have already refreshed.
            if (!sessionManager.isTokenExpiringSoon()) return@withLock
            val refreshToken = sessionManager.getRefreshToken() ?: return@withLock

            withContext(RefreshGuard()) {
                Log.d(TAG, "Connection about to expire - refreshing")
                val result = authRepository.refreshToken(refreshToken, deviceIdProvider())
                when (result) {
                    is Result.Success -> Log.d(TAG, "Connection refreshed")
                    is Result.Failure -> {
                        Log.w(TAG, "Connection could not be refreshed (${result.error}). Signing out.")
                        sessionManager.logout()
                    }
                }
            }
        }
    }

    private class RefreshGuard : AbstractCoroutineContextElement(Key) {
        companion object Key : CoroutineContext.Key<RefreshGuard>
    }

    private companion object {
        const val TAG = "TokenRefresher"
    }
}
