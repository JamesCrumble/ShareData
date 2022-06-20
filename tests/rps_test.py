import time
import orjson
import requests

DESTINATION = 'http://192.168.0.25'
PORT = 50000
ENDPOINT = 'get_content'

URL = f'{DESTINATION}:{PORT}/{ENDPOINT}'

print(orjson.loads(requests.get(URL).text))

total_per_test = list()
total_rpses = list()
TEST_TIME = 20  # sec
TESTS_COUNT = 6
WITH_SERIALIZING = True


for _ in range(TESTS_COUNT):
    test_start = time.time()

    while time.time() - test_start < TEST_TIME:

        execute_time = time.time()
        if WITH_SERIALIZING:
            orjson.loads(requests.get(URL).text)
        else:
            requests.get(URL)
        end_time = time.time()

        total_rpses.append(1 / (end_time - execute_time))

    total_per_test.append(sum(total_rpses) / len(total_rpses))
    total_rpses = list()

print(
    f'avg RPSes: {total_per_test} for {TESTS_COUNT} times by {TEST_TIME} seconds of tests'
)
