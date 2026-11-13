#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo "== Сборка =="
dotnet build Semestrovka_I.sln --nologo

echo
echo "== Тесты =="
dotnet test Tests/Tests.csproj --nologo

echo
echo "Готово. Для демонстрации:"
echo "  1. Запусти Server."
echo "  2. Запусти Courier с профилем courier1."
echo "  3. В окне нажми «Выйти на линию»."
echo "  4. В консоли сервера создай заказ."
