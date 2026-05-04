package com.goldbank.shared.data.remote

import com.goldbank.shared.domain.util.AppError
import com.goldbank.shared.domain.util.Result
import io.grpc.StatusException
import io.grpc.StatusRuntimeException
import org.koin.core.context.GlobalContext

/**
 * Wraps a suspending gRPC stub call into a [Result] and ensures the access
 * token is fresh before the request goes out. Token refresh is best-effort —
 * if Koin isn't initialised (tests, early boot) the call still proceeds with
 * whatever token is currently in [com.goldbank.shared.data.local.SessionManager].
 */
suspend fun <T> grpcCall(block: suspend () -> T): Result<T> {
    GlobalContext.getOrNull()?.getOrNull<TokenRefresher>()?.ensureFresh()
    return try {
        Result.Success(block())
    } catch (e: StatusException) {
        Result.Failure(GrpcErrorMapper.map(e))
    } catch (e: StatusRuntimeException) {
        Result.Failure(GrpcErrorMapper.map(e))
    } catch (e: Exception) {
        Result.Failure(AppError.Unknown(e))
    }
}
