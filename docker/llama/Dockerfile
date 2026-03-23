FROM debian:bookworm-slim

ARG LLAMA_CPP_REPO=https://github.com/ggerganov/llama.cpp.git
ARG LLAMA_CPP_REF=master
ARG GGML_NATIVE=OFF

WORKDIR /opt

RUN apt-get update \
    && apt-get install -y --no-install-recommends build-essential ca-certificates cmake curl git libopenblas-dev pkg-config \
    && rm -rf /var/lib/apt/lists/*

RUN git clone --depth 1 --branch ${LLAMA_CPP_REF} ${LLAMA_CPP_REPO} llama.cpp

WORKDIR /opt/llama.cpp

RUN cmake -B build -DGGML_NATIVE=${GGML_NATIVE} -DGGML_BLAS=ON -DGGML_BLAS_VENDOR=OpenBLAS -DLLAMA_BUILD_SERVER=ON \
    && cmake --build build --config Release -j"$(nproc)"

ENV MODEL_PATH=/models/llama/model.gguf
ENV LLAMA_PORT=8080
ENV N_CTX=2048

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --retries=5 \
  CMD curl -fsS http://localhost:8080/health || exit 1

CMD ["/bin/sh", "-c", "./build/bin/llama-server -m ${MODEL_PATH} --host 0.0.0.0 --port ${LLAMA_PORT} -c ${N_CTX}"]
