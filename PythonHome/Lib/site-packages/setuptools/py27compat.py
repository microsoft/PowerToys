"""
Compatibility Support for Python 2.7 and earlier
"""

import sys

def get_all_headers(message, key):
	"""
	Given an HTTPMessage, return all headers matching a given key.
	"""
	return message.get_all(key)

if sys.version_info < (3,):
	def get_all_headers(message, key):
		return message.getheaders(key)
