FROM debian:bookworm-slim

ARG WHISPER_CPP_REPO=https://github.com/ggerganov/whisper.cpp.git
ARG WHISPER_CPP_REF=master

WORKDIR /opt

RUN apt-get update \
    && apt-get install -y --no-install-recommends build-essential cmake curl ca-certificates git ffmpeg \
    && rm -rf /var/lib/apt/lists/*

RUN git clone --depth 1 --branch ${WHISPER_CPP_REF} ${WHISPER_CPP_REPO} whisper.cpp

WORKDIR /opt/whisper.cpp

RUN cmake -B build -DGGML_NATIVE=OFF -DWHISPER_BUILD_SERVER=ON \
    && cmake --build build --config Release -j"$(nproc)"

ENV MODEL_PATH=/models/whisper/ggml-base.en.bin
ENV WHISPER_PORT=8080

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --retries=5 \
  CMD curl -fsS http://localhost:8080/ || exit 1

CMD ["/bin/sh", "-c", "./build/bin/whisper-server --host 0.0.0.0 --port ${WHISPER_PORT} --model ${MODEL_PATH}"]
