# Докер-образ QP.GraphQL

## Назначение

Образ содержит сервис QP.GraphQL, которое устанавливается как модуль к продукту **QP8.CMS c поддержкой PostgreSQL**. Использование образа описано в [руководстве пользователя QP8.GraphQL](https://storage.qp.qsupport.ru/qa_official_site/images/downloads/qp8-graphql-user-man.pdf) (в разделе **Установка**).

## Репозитории

* [DockerHub](https://hub.docker.com/r/qpcms/qp-graphql-service/tags): `qpcms/qp-graphql-service`
* QA Harbor: `registry.quantumart.ru/qp8-cms/graphql`

## История тегов (версий)

### 1.1.0.3

* Добавлена настройка EnableGraphqlUI (#172900)

### 1.1.0.0

* Поддержка `Contains` и `NotContains` в O2M (#172695)
* Поддержка `Contains` и `NotContains` в M2M (#163109)
* Исправлено получение M2M (#172492)
* Добавлено кэширование (и настройка `CacheLifeTime`)

### 1.0.8.7

* Обновление до .NET 6

### 1.0.7.3

* Поддержка фильтров `IsNull`, `In`, `NotIn`, `Contains`, `NotContains`
* Добавлены лимиты (#163105)
