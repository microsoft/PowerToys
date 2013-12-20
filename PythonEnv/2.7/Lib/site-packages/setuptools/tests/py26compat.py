import unittest

try:
	# provide skipIf for Python 2.4-2.6
	skipIf = unittest.skipIf
except AttributeError:
	def skipIf(condition, reason):
		def skipper(func):
			def skip(*args, **kwargs):
				return
			if condition:
				return skip
			return func
		return skipper
