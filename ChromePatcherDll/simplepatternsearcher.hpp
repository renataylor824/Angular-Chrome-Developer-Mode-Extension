#pragma once

namespace ChromePatch {
	class
#ifdef _DEBUG
	__declspec(dllexport)
#endif
	SimplePatternSearcher : PatternSearcher {
	public:
		byte* SearchBytePattern(Patch& patch, byte* startAddr, size_t length) override;
	};
}
