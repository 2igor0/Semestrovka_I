#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
    echo "Запусти скрипт через sudo:"
    echo "  sudo bash scripts/loss-demo.sh"
    exit 1
fi

if ! command -v tc >/dev/null 2>&1; then
    echo "Утилита tc не найдена. Установи iproute2 или используй ручной сценарий."
    exit 1
fi

cleanup() {
    tc qdisc del dev lo root 2>/dev/null || true
    echo
    echo "Потери и задержка отключены."
}

trap cleanup EXIT INT TERM

tc qdisc replace dev lo root netem delay 100ms loss 30% duplicate 10%

echo "На loopback включены:"
echo "  задержка 100 ms"
echo "  потери 30%"
echo "  дубли 10%"
echo
echo "Теперь запусти сервер и курьера, создай заказ и наблюдай повторы."
echo "Нажми Enter, чтобы выключить имитацию."
read -r
