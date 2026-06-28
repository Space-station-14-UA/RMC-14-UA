# List Commendations Command
cmd-rmclistcommendations-desc = Показує список нагород за раундом, гравцем, id або нещодавніми записами.
cmd-rmclistcommendations-help = Використання:
  rmclistcommendations last <count> [type]
    - Показує список найнещодавніших нагород
    - count: кількість найнещодавніших нагород для показу
    - type: фільтр типу нагород (всі за замовчуванням)

  rmclistcommendations round <roundId> [type]
    - Показує список всіх нагород для специфічного раунду
    - type: фільтр типу нагород (всі за замовчуванням)

  rmclistcommendations id <commendationId>
    - Показує одну нагороду за id

  rmclistcommendations player giver <usernameOrId> <count> [type]
    - Показує список нагород виданих гравцем
    - count: кількість найнещодавніших нагород для показу
    - type: фільтр типу нагород (всі за замовчуванням)

  rmclistcommendations player receiver <usernameOrId> <count> [type]
    - Показує список нагород отриманих гравцем
    - count: кількість найнещодавніших нагород для показу
    - type: фільтр типу нагород (всі за замовчуванням)

  Приклади:
    rmclistcommendations last 10
    rmclistcommendations last 5 jelly
    rmclistcommendations round 42
    rmclistcommendations round 42 medal
    rmclistcommendations id 128
    rmclistcommendations player giver PlayerName 10
    rmclistcommendations player receiver PlayerName 5 jelly

# Errors
cmd-rmclistcommendations-invalid-arguments = Некоректні аргументи!
cmd-rmclistcommendations-invalid-round-id = Недійсний ID раунду!
cmd-rmclistcommendations-invalid-id = Недійсний ID нагороди!
cmd-rmclistcommendations-invalid-type = Недійсний тип '{ $type }'!
cmd-rmclistcommendations-invalid-player-mode = Недійсний тип гравця! Має бути 'giver' або 'receiver'.
cmd-rmclistcommendations-invalid-count = Недійсна кількість! Має бути натуральним числом.
cmd-rmclistcommendations-player-not-found = Гравця '{ $player }' не знайдено.
cmd-rmclistcommendations-no-results = Не знайдено нагород.

# Headers
cmd-rmclistcommendations-last-header = Показ { $count } найнещодавніших нагород (requested: { $total }):
cmd-rmclistcommendations-round-header = Нагороди для Раунду { $round } ({ $count } total):
cmd-rmclistcommendations-id-header = Нагорода { $id }:
cmd-rmclistcommendations-giver-header = Показ { $count } найнещодавніших виданих нагород (requested: { $total }):
cmd-rmclistcommendations-receiver-header = Показ { $count } найнещодавніших отриманих нагород (requested: { $total }):

# Format
cmd-rmclistcommendations-format = id [{ $id }] { $type }: { $name } - { $giverUserName } ({ $giver }) → { $receiverUserName } ({ $receiver }) Round { $round }: { $text }

# Completion hints
cmd-rmclistcommendations-hint-mode = Режим (last, round, id, або player)
cmd-rmclistcommendations-hint-mode-last = Показує список найнещодавніших нагород
cmd-rmclistcommendations-hint-mode-round = Показує список нагород за раундом
cmd-rmclistcommendations-hint-mode-id = Показує список нагород за Id
cmd-rmclistcommendations-hint-mode-player = Показує список нагород за гравцем
cmd-rmclistcommendations-hint-round-id = ID Раунду
cmd-rmclistcommendations-hint-commendation-id = ID Нагороди
cmd-rmclistcommendations-hint-player-mode = Тип гравця (giver або receiver)
cmd-rmclistcommendations-hint-player-giver = Нагороди видані гравцем
cmd-rmclistcommendations-hint-player-receiver = Нагороди отримані гравцем
cmd-rmclistcommendations-hint-player = Ім'я користувача або UserId
cmd-rmclistcommendations-hint-count = Кількість нагород для показу
cmd-rmclistcommendations-hint-type = Фільтр типу нагород
