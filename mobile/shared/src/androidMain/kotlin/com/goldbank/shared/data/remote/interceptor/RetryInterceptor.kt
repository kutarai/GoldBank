package com.goldbank.shared.data.remote.interceptor

import io.grpc.CallOptions
import io.grpc.Channel
import io.grpc.ClientCall
import io.grpc.ClientInterceptor
import io.grpc.ForwardingClientCall
import io.grpc.ForwardingClientCallListener
import io.grpc.Metadata
import io.grpc.MethodDescriptor
import io.grpc.Status

class RetryInterceptor(
    private val maxRetries: Int = 3,
) : ClientInterceptor {

    private val retryableStatusCodes = setOf(
        Status.Code.UNAVAILABLE,
        Status.Code.DEADLINE_EXCEEDED,
    )

    override fun <ReqT, RespT> interceptCall(
        method: MethodDescriptor<ReqT, RespT>,
        callOptions: CallOptions,
        next: Channel,
    ): ClientCall<ReqT, RespT> {
        // For streaming calls, don't retry
        if (method.type != MethodDescriptor.MethodType.UNARY) {
            return next.newCall(method, callOptions)
        }
        return next.newCall(method, callOptions)
    }
}
