#!/usr/bin/env bash
# Gera o pacote drop-in para Unity em ./dist/
#
# Por que script em vez de commitar dist/:
#   DLLs binárias mudam a cada build (timestamps, deps NuGet); inflam o repo
#   e criam diff ruim. Mais fácil regenerar: dotnet publish + copy.
#
# Uso:
#   ./scripts/build-unity-dist.sh
#
# Saída: ./dist/Plugins/LightOrm/{LightOrm.Core.dll,Sqlite/,MySql/,Postgres/}
#        ./dist/Scripts/LightOrm/{DatabaseManager.cs,UnityUsageExample.cs}
#        ./dist/README.md (instruções de instalação)

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST="$ROOT/dist"
TMP="$(mktemp -d)"

echo "==> Limpando $DIST"
rm -rf "$DIST"
mkdir -p "$DIST/Plugins/LightOrm/Sqlite" \
         "$DIST/Plugins/LightOrm/MySql" \
         "$DIST/Plugins/LightOrm/Postgres" \
         "$DIST/Scripts/LightOrm"

echo "==> dotnet publish dos providers (Release, netstandard2.1)"
for prov in Sqlite MySql Postgres; do
    dotnet publish "$ROOT/LightOrm.$prov/LightOrm.$prov.csproj" \
        -c Release -o "$TMP/$prov" --nologo > /dev/null
done

# DLLs que conflitam com Unity 6 (já vêm embutidos no scripting runtime).
# Removê-los do drop-in evita "Lifecycle ERROR: TopologicalSort NullReferenceException".
SKIP=(
    "System.Buffers.dll"
    "System.Memory.dll"
    "System.Numerics.Vectors.dll"
    "System.Runtime.CompilerServices.Unsafe.dll"
    "Microsoft.Bcl.AsyncInterfaces.dll"
)

contains() { local n="$1"; shift; for x in "$@"; do [[ "$x" == "$n" ]] && return 0; done; return 1; }

echo "==> Copiando LightOrm.Core.dll"
cp "$TMP/Sqlite/LightOrm.Core.dll" "$DIST/Plugins/LightOrm/"

for prov in Sqlite MySql Postgres; do
    echo "==> Filtrando DLLs de $prov"
    for dll in "$TMP/$prov"/*.dll; do
        name="$(basename "$dll")"
        # Core já foi copiado para a raiz.
        [[ "$name" == "LightOrm.Core.dll" ]] && continue
        # Pula System.* / Microsoft.Bcl.* que conflitam com Unity 6.
        if contains "$name" "${SKIP[@]}"; then continue; fi
        cp "$dll" "$DIST/Plugins/LightOrm/$prov/"
    done
done

echo "==> Copiando native SQLite (Win/Linux/macOS x64 + macOS arm64)"
SQLITE_NATIVE_BASE="$HOME/.nuget/packages/sqlitepclraw.lib.e_sqlite3"
SQLITE_VERSION="$(ls -1 "$SQLITE_NATIVE_BASE" 2>/dev/null | sort -V | tail -1 || true)"
if [[ -n "$SQLITE_VERSION" ]]; then
    SRC="$SQLITE_NATIVE_BASE/$SQLITE_VERSION/runtimes"
    for rid in win-x64 linux-x64 osx-x64 osx-arm64; do
        ext="dll"
        [[ "$rid" == linux* ]] && ext="so" && file="libe_sqlite3.so"
        [[ "$rid" == osx*   ]] && ext="dylib" && file="libe_sqlite3.dylib"
        [[ "$rid" == win*   ]] && file="e_sqlite3.dll"
        if [[ -f "$SRC/$rid/native/$file" ]]; then
            mkdir -p "$DIST/Plugins/LightOrm/Sqlite/runtimes/$rid/native"
            cp "$SRC/$rid/native/$file" "$DIST/Plugins/LightOrm/Sqlite/runtimes/$rid/native/"
        fi
    done
else
    echo "    AVISO: SQLitePCLRaw native não encontrado em ~/.nuget. Build pode falhar em runtime no Unity."
fi

echo "==> Copiando scripts"
# DatabaseManager simplificado (Sqlite-only) e exemplo
cp "$ROOT/LightOrm.Unity/Database/DatabaseManager.cs" "$DIST/Scripts/LightOrm/"
cp "$ROOT/LightOrm.Unity/Examples/UnityUsageExample.cs" "$DIST/Scripts/LightOrm/"

echo "==> Gerando dist/README.md"
cat > "$DIST/README.md" <<'EOF'
# LightOrm — Drop-in package para Unity

> **Validado em Unity 6 (6000.4.5f1)** com SQLite local e PostgreSQL remoto rodando em jogo.

## Como instalar

1. Copie o conteúdo:
   - `dist/Plugins/`   →  `YourUnityProject/Assets/Plugins/`
   - `dist/Scripts/`   →  `YourUnityProject/Assets/Scripts/` (ou onde preferir)
2. Crie GameObject "DatabaseManager" e adicione o componente.
3. Provider = SQLite (default). Roda.

## Backends suportados

| Backend | Funciona? | Tamanho | Quando usar |
|---|---|---|---|
| SQLite | ✅ | ~1.7 MB | Save local de jogo |
| PostgreSQL | ✅ | ~3 MB | Backend remoto (cuidado com credencial exposta no cliente) |
| MySQL | Não testado em Unity | ~5 MB | Idem |
| MongoDB | ❌ | — | Driver tem deps incompatíveis (mongocrypt, AWSSDK). Use no servidor. |

Se só usa SQLite, delete `MySql/` e `Postgres/` para reduzir para ~1.7MB.

## Solução de problemas

| Sintoma | Fix |
|---|---|
| `Lifecycle ERROR: TopologicalSort NullReferenceException` | DLL `System.*` duplicada — apague da pasta do provider (Unity já tem) |
| `Unable to resolve reference 'X'` | DLL dependência faltando — rode o script de novo |
| `CS2001: Source file ... could not be found` | Feche Unity, apague `Library/Bee/` e `Library/ScriptAssemblies/`, reabra |
| `dynamic` exige `Microsoft.CSharp.dll` | Use `GetRepositoryAsync<T, TId>()` (2 parâmetros), não `<T>` |

Veja README do repo para guia completo da API e exemplos.
EOF

rm -rf "$TMP"

echo ""
echo "==> Pronto! $DIST"
du -sh "$DIST/Plugins" 2>/dev/null
echo "Copie dist/Plugins/ e dist/Scripts/ para Assets/ do seu projeto Unity."
