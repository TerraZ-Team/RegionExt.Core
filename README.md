# RegionExt.Core

Базовый модуль Region Extension для TShock.

`RegionExt.Core` содержит основную логику работы с регионами, историю изменений, контексты команд и публичные точки расширения для модулей (`RegionExt.Triggers`, `RegionExt.Requests`).

## Что делает модуль

- Расширяет команды работы с регионами.
- Добавляет команды для владельцев регионов.
- Ведет историю действий над регионами (`undo/redo/restore`).
- Поддерживает контекстные аргументы (`$this`, `$myname`, `$near`).
- Регистрирует инфраструктуру, к которой подключаются внешние модули (дополнительные subcommands и root-команды).

## Команды

### `/region`, `/re`

Основные команды:

- `set <1/2>`
- `clear`
- `define`, `d <name>`
- `delete`, `del [region]`
- `name`, `n [x] [y]`
- `rename`, `rn <region> <newname>`
- `list`, `l [page]`
- `resize`, `rs`, `expand`, `exp <region> <amount> <u/d/r/l>`
- `allow`, `a <user> [region]`
- `remove`, `r <user> [region]`
- `allowg`, `ag <group> [region]`
- `removeg`, `rg <group> [region]`
- `info`, `i [region] [page]`
- `protect`, `p [region] [true/false]`
- `z [region] [z]`
- `tp [region]`
- `move`, `mv <region> <amount> <u/d/r/l>`
- `setowner`, `so [user] [region]`
- `clearmembers`, `cm [region]`
- `fastregion`, `fr <name> [owner] [z] [protect]`
- `fastregionbreak`, `frb`
- `ownerlist`, `ol [user] [page]`
- `allowedlist`, `al [user] [page]`
- `listact`, `la [page]`

### `/regionown`, `/ro`

Команды владельца региона:

- `setowner`, `so [user] [region]`
- `clearmembers`, `cm [region]`
- `ownerlist`, `ol [page]`
- `allow`, `a <user> [region]`
- `remove`, `r <user> [region]`
- `info`, `i [region] [page]`
- `set <1/2>`
- `delete`, `del [region]`
- `fastregionbreak`, `frb`

### `/regionhistory`, `/rh`

История регионов:

- `undo`, `u <count> [region]`
- `redo`, `r <count> [region]`
- `restore`, `res <region>`
- `restoreuser`, `resu <user> [count]`
- `history`, `h [page] [region]`
- `dellist`, `dl [page]`

### Вспомогательные команды

- `context [page]` - список контекстных команд.
- `reperm [page]` - список permissions модуля.
- `reloc [EN|RU]` - переключение локализации игрока.

## Контекстные аргументы

- `$this`, `$t` - текущий регион игрока.
- `$myname`, `$mn` - аккаунт игрока.
- `$near`, `$n` - ближайший игрок.

Пример:

- `/region info $this`

## Permissions

- `tshock.admin.region` - доступ к `/region` и `/re`.
- `regionext.own` - доступ к `/regionown` и `/ro`.
- `regionext.history` - доступ к `/regionhistory` и `/rh`.

## Конфиг

Файл: `tshock/RegionExtension.json`

```json
{
  "ContextSpecifier": "$",
  "ContextAllow": true,
  "AutoCompleteSameName": true,
  "AutoCompleteSameNameFormat": "{0}:{1}",
  "DefaultLocalization": "EN"
}
```

## Интеграция модулей

`RegionExt.Core` позволяет внешним модулям:

- добавлять root-команды;
- добавлять subcommands в `/region` и `/regionown`;
- подписываться на события операций над регионами (до/после).

За счет этого `RegionExt.Triggers` и `RegionExt.Requests` подключаются без правок в core-коде.

## Сборка

```powershell
dotnet build src/RegionExt.Core.csproj
```
