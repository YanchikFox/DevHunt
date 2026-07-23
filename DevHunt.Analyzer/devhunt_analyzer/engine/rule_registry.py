from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from devhunt_analyzer.rules.base import BaseRule
    from devhunt_analyzer.engine.models import Category, Language

_REGISTRY: list[type[BaseRule]] = []


def register(cls: type[BaseRule]) -> type[BaseRule]:
    """Decorator to register a rule class."""
    _REGISTRY.append(cls)
    return cls


def get_all_rules() -> list[BaseRule]:
    return [cls() for cls in _REGISTRY]


def get_rules_for_language(lang: Language) -> list[BaseRule]:
    from devhunt_analyzer.engine.models import Language as Lang
    return [
        cls() for cls in _REGISTRY
        if lang in cls.languages or Lang.ANY in cls.languages
    ]


def get_rules_by_category(cat: Category) -> list[BaseRule]:
    return [cls() for cls in _REGISTRY if cls.category == cat]


def list_rules() -> list[dict]:
    return [
        {
            "rule_id": cls.rule_id,
            "name": cls.name,
            "severity": cls.severity.value,
            "category": cls.category.value,
            "languages": [lang.value for lang in cls.languages],
            "cwe_id": cls.cwe_id,
            "description": cls.description,
        }
        for cls in sorted(_REGISTRY, key=lambda c: c.rule_id)
    ]
