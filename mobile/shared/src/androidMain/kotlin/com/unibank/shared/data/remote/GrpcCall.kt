package com.unibank.shared.data.remote

import com.unibank.shared.domain.util.AppError
import com.unibank.shared.domain.util.Result
import io.grpc.StatusException
import io.grpc.StatusRuntimeException

suspend fun <T> grpcCall(block: suspend () -> T): Result<T> = try {
    Result.Success(block())
} catch (e: StatusException) {
    Result.Failure(GrpcErrorMapper.map(e))
} catch (e: StatusRuntimeException) {
    Result.Failure(GrpcErrorMapper.map(e))
} catch (e: Exception) {
    Result.Failure(AppError.Unknown(e))
}
