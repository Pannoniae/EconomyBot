"""
analysis engine
"""
import inspect


def is_transformation(transformation) -> bool:

    """
    a valid transformation is a callable that has a string input and a string output
    """

    result = True
    if inspect.isfunction(transformation):
        parameters = inspect.signature(transformation).parameters
        if len(parameters) != 1:
            result = False
        try:
            type_ = parameters["input_text"].annotation
            if type_ != str:
                result = False
        except KeyError:
            result = False
    else:
        result = False

    # TODO allow callable classes, too
    return result


class Engine:
    """
    Performs grammatical analysis and transformations
    """

    def __init__(self):
        self._transformations = []

    def register_transformation(self, transformation):
        # TODO check if this is a transformation function
        if is_transformation(transformation):
            self._transformations.append(transformation)
        else:
            raise TypeError("Wrong transformation")

    def __call__(self, input_text: str) -> str:
        result = input_text
        for transformation in self._transformations:
            result = transformation(result)
        return result
