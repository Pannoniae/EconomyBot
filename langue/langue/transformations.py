"""
common text transformations
"""

import re


def replace_word(input_text: str, source_word: str, target_word) -> str:
    return re.sub(rf"\b{source_word}\b", target_word, input_text)
