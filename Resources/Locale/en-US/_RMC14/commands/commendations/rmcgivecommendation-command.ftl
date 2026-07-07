# Give Commendation Command
cmd-rmcgivecommendation-desc = Нагороджує гравця медаллю або желе
cmd-rmcgivecommendation-help = Використання: rmcgivecommendation <giverName> <receiver> <receiverName> <type> <commendationType> <citation> [roundId]
  Arguments:
  giverName: хто видає нагороду IC (ПОВИННО використовувати лапки якщо містить пропуски)
  receiver: ім'я користувача або UserId гравця
  receiverName: ім'я персонажа (ПОВИННО використовувати лапки якщо містить пропуски)
  type: medal або jelly
  commendationType: число (скористайтеся автозавершенням за допомогою клавіші Tab, щоб переглянути доступні типи)
  citation: причина нагородження (ПОВИННА використовувати лапки)
  roundId: номер раунду, за замовчуванням поточний раунд (опціонально)

  Приклади:
    rmcgivecommendation "Вище Командування КМПОН" PlayerName "Джон Трейзен" medal 1 "За вийняткову хоробрість"
    rmcgivecommendation "Королева-Матір" XenoPlayer "XX-Alpha" jelly 2 "За захист Вулику"
    rmcgivecommendation "Вище Командування КМПОН" PlayerName "Джон Трейзен" medal 1 "За вийняткову хоробрість" 42

# Errors
cmd-rmcgivecommendation-invalid-arguments = Некоректна кількість аргументів!
cmd-rmcgivecommendation-invalid-type = Недійсний тип! Має бути 'medal' або 'jelly'.
cmd-rmcgivecommendation-invalid-award-type = Недійсний '{ $type }' тип! Має бути 1-{ $max }.
cmd-rmcgivecommendation-empty-citation = Причина нагородження не може бути пустою!
cmd-rmcgivecommendation-player-not-found = Гравця '{ $player }' не знайдено.

# Success
cmd-rmcgivecommendation-success = { $award } нагороджено { $player }!
cmd-rmcgivecommendation-admin-announcement = { $admin } нагородив { $type } "{ $award }" гравця { $receiver } (character: { $character }) для раунду { $round }

# Completion hints
cmd-rmcgivecommendation-hint-giver = IC ім'я того, хто видає нагороду (будьте обережні вводячи IC ім'я)
cmd-rmcgivecommendation-hint-giver-highcommand = Стандартний видавець медалей для морпіхів
cmd-rmcgivecommendation-hint-giver-queen-mother = Стандартний видавець желе для ксеноїдів
cmd-rmcgivecommendation-hint-receiver = Ім'я користувача або UserId
cmd-rmcgivecommendation-hint-receiver-name = Ім'я персонажа отримувача (будьте обережні вводячи IC ім'я)
cmd-rmcgivecommendation-hint-type = Типу (medal або jelly)
cmd-rmcgivecommendation-hint-type-medal = Нагородити морпіха медаллю
cmd-rmcgivecommendation-hint-type-jelly = Нагородити ксено материнським желе
cmd-rmcgivecommendation-hint-medal-type = Тип медалі (1-{ $count })
cmd-rmcgivecommendation-hint-jelly-type = Тип желе (1-{ $count })
cmd-rmcgivecommendation-hint-invalid-type = Тип має бути 'medal' або 'jelly'
cmd-rmcgivecommendation-hint-citation = Причина нагородження (будьте обережні вводячи IC причини)
cmd-rmcgivecommendation-hint-round = ID Раунду (опціонально)
cmd-rmcgivecommendation-hint-round-current = Поточний раунд
