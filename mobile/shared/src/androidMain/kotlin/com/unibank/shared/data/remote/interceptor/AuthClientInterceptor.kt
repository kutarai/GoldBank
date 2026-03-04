package com.unibank.shared.data.remote.interceptor

import io.grpc.CallOptions
import io.grpc.Channel
import io.grpc.ClientCall
import io.grpc.ClientInterceptor
import io.grpc.ForwardingClientCall
import io.grpc.Metadata
import io.grpc.MethodDescriptor

class AuthClientInterceptor(
    private val tokenProvider: () -> String?,
    private val tenantIdProvider: () -> String,
) : ClientInterceptor {

    private val authKey = Metadata.Key.of("authorization", Metadata.ASCII_STRING_MARSHALLER)
    private val tenantKey = Metadata.Key.of("x-tenant-id", Metadata.ASCII_STRING_MARSHALLER)

    override fun <ReqT, RespT> interceptCall(
        method: MethodDescriptor<ReqT, RespT>,
        callOptions: CallOptions,
        next: Channel,
    ): ClientCall<ReqT, RespT> {
        return object : ForwardingClientCall.SimpleForwardingClientCall<ReqT, RespT>(
            next.newCall(method, callOptions)
        ) {
            override fun start(responseListener: Listener<RespT>, headers: Metadata) {
                tokenProvider()?.let { token ->
                    headers.put(authKey, "Bearer $token")
                }
                headers.put(tenantKey, tenantIdProvider())
                super.start(responseListener, headers)
            }
        }
    }
}
