cmd-rmcdeletecommendations-desc = Видаляє нагороду за раундом, автором, одержувачем або id.
cmd-rmcdeletecommendations-help = Використання:
  rmcdeletecommendations id <commendationId>
    - Видаляє одну нагороду за id

  rmcdeletecommendations round <roundId> <type>
    - Видаляє всі нагороди для специфічного раунду та типу
    - type: фільтр типу нагород

  rmcdeletecommendations round <roundId> <type> giver <usernameOrId>
    - Видаляє нагороди для раунду та типу видані гравцем
    - type: фільтр типу нагород

  rmcdeletecommendations round <roundId> <type> receiver <usernameOrId>
    - Видаляє негороди для раунду та типу отримані гравцем
    - type: фільтр типу нагород

  Приклади:
    rmcdeletecommendations id 128
    rmcdeletecommendations round 42 medal
    rmcdeletecommendations round 42 jelly giver PlayerName
    rmcdeletecommendations round 42 medal receiver PlayerName

cmd-rmcdeletecommendations-invalid-arguments = Некоректні аргументи!
cmd-rmcdeletecommendations-invalid-round-id = Недійсне ID раунду!
cmd-rmcdeletecommendations-invalid-id = Недійсне ID нагороди!
cmd-rmcdeletecommendations-invalid-type = Недійсний тип '{ $type }'!
cmd-rmcdeletecommendations-invalid-player-mode = Недійсний тип гравця! Має бути 'giver' та 'receiver'.
cmd-rmcdeletecommendations-player-not-found = Гравця '{ $player }' не знайдено.
cmd-rmcdeletecommendations-no-results = Нагород не знайдено.

cmd-rmcdeletecommendations-id-header = Видалено нагороду { $id }:
cmd-rmcdeletecommendations-round-header = Видалено нагороди для раунду { $round } ({ $count } total):
cmd-rmcdeletecommendations-format = id [{ $id }] { $type }: { $name } - { $giverUserName } ({ $giver }) → { $receiverUserName } ({ $receiver }) Round { $round }: { $text }
cmd-rmcdeletecommendations-admin-announcement = { $admin } видалив нагороди з ID: { $ids }
cmd-rmcdeletecommendations-admin-announcement-round = { $admin } видавлив нагороди для раунду { $round } з ID: { $ids }

cmd-rmcdeletecommendations-hint-mode = Режим (id or round)
cmd-rmcdeletecommendations-hint-mode-id = Видалити нагороду за id
cmd-rmcdeletecommendations-hint-mode-round = Видалити нагороди за раундом
cmd-rmcdeletecommendations-hint-round-id = ID Раунду
cmd-rmcdeletecommendations-hint-commendation-id = ID Нагороди
cmd-rmcdeletecommendations-hint-type = Тип нагороди
cmd-rmcdeletecommendations-hint-player-mode = Тип гравця (giver or receiver)
cmd-rmcdeletecommendations-hint-player-giver = Нагороди видані гравцем
cmd-rmcdeletecommendations-hint-player-receiver = Нагороди отримані гравцем
cmd-rmcdeletecommendations-hint-player = Ім'я користувача або UserId гравця
